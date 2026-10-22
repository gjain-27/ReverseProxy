using Proxy.LoadBalancing;
using Proxy.RateLimiting;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Proxy
{
    public class TcpProxy
    {
        private readonly int _listenPort;
        private readonly ILoadBalancer _balancer;
        private readonly RateLimiter _rateLimiter;
        private readonly X509Certificate2? _certificate;

        private const int MaxConnectAttempts = 3;
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);

        public TcpProxy(int listenPort, ILoadBalancer balancer, RateLimiter rateLimiter, X509Certificate2? certificate = null)
        {
            _listenPort = listenPort;
            _balancer = balancer;
            _rateLimiter = rateLimiter;
            _certificate = certificate;
        }

        public async Task StartAsync()
        {
            TcpListener listener = TcpListener.Create(_listenPort);

            listener.Start();

            Console.WriteLine($"Listening on port {_listenPort}");

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client);
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    Console.WriteLine("Client connected");

                    using Stream clientStream = await OpenClientStreamAsync(client);
                    IPAddress clientIp = ((IPEndPoint)client.Client.RemoteEndPoint!).Address;

                    RequestReader reader = new RequestReader(clientStream);

                    ReadOnlyMemory<byte>? head = await reader.ReadHeadAsync();
                    if (head == null)
                    {
                        return;
                    }

                    if (!_rateLimiter.TryAcquire(clientIp))
                    {
                        Console.WriteLine($"Rate limited {clientIp}");
                        await WriteErrorResponseAsync(clientStream, 429, "Too Many Requests", "Rate limit exceeded");
                        return;
                    }

                    var connection = await ConnectToBackendAsync();
                    if (connection == null)
                    {
                        Console.WriteLine("No healthy backends");
                        await WriteErrorResponseAsync(clientStream, 503, "Service Unavailable", "No healthy backends");
                        return;
                    }

                    Backend target = connection.Value.Target;
                    using TcpClient backend = connection.Value.Client;

                    target.OnConnectionOpened();
                    try
                    {
                        Console.WriteLine($"Forwarding to {target}");

                        NetworkStream backendStream = backend.GetStream();

                        Task requestTask = ForwardRequestsAsync(reader, head.Value, backendStream, clientIp);
                        Task responseTask = backendStream.CopyToAsync(clientStream);

                        Task finished = await Task.WhenAny(requestTask, responseTask);
                        await finished;

                        Console.WriteLine("Connection closed");
                    } finally
                    {
                        target.OnConnectionClosed();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Connection error: {e.Message}");
                }
            }
        }

        private async Task<Stream> OpenClientStreamAsync(TcpClient client)
        {
            NetworkStream networkStream = client.GetStream();

            if (_certificate == null)
            {
                return networkStream;
            }

            SslStream ssl = new SslStream(networkStream);
            try
            {
                using CancellationTokenSource timeout = new CancellationTokenSource(HandshakeTimeout);
                await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = _certificate }, timeout.Token);
                return ssl;
            }
            catch
            {
                ssl.Dispose();
                throw;
            }
        }

        private async Task<(Backend Target, TcpClient Client)?> ConnectToBackendAsync()
        {
            for (int attempt = 0; attempt < MaxConnectAttempts; attempt++)
            {
                Backend? target = _balancer.Pick();
                if (target == null)
                {
                    return null;
                }

                TcpClient client = new TcpClient();
                try
                {
                    using CancellationTokenSource timeout = new CancellationTokenSource(ConnectTimeout);
                    await client.ConnectAsync(target.Host, target.Port, timeout.Token);
                    return (target, client);
                }
                catch (Exception e) when (e is SocketException || e is OperationCanceledException)
                {
                    client.Dispose();
                    Console.WriteLine($"Could not connect to {target}: {e.Message}");
                    target.MarkUnhealthy();
                }
            }

            return null;
        }

        private async Task ForwardRequestsAsync(RequestReader reader, ReadOnlyMemory<byte> firstHead, Stream backendStream, IPAddress clientIp)
        {
            ReadOnlyMemory<byte>? head = firstHead;

            while (head != null)
            {
                string headText = Encoding.ASCII.GetString(head.Value.Span);
                HttpRequest request = HttpParser.Parse(headText);

                Console.WriteLine($"Method: {request.Method}, Path: {request.Path}, Version: {request.Version}");

                long bodyLength = HttpParser.GetBodyLength(request);
                byte[] forwardedHead = ForwardedHeaders.Rewrite(headText, clientIp, _certificate == null ? "http" : "https");
                await backendStream.WriteAsync(forwardedHead);
                await reader.CopyBodyAsync(bodyLength, backendStream);

                head = await reader.ReadHeadAsync();

                if (head != null)
                {
                    while (!_rateLimiter.TryAcquire(clientIp))
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(50));
                    }
                }
            }
        }

        private static async Task WriteErrorResponseAsync(Stream stream, int statusCode, string reason, string message)
        {
            byte[] body = Encoding.ASCII.GetBytes(message);

            string head =
                $"HTTP/1.1 {statusCode} {reason}\r\n" +
                "Content-Type: text/plain\r\n" +
                $"Content-Length: {body.Length}\r\n" +
                "Connection: close\r\n" +
                "\r\n";

            await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
            await stream.WriteAsync(body);
        }
    }
}