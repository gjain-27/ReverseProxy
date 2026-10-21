using Proxy.LoadBalancing;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Proxy
{
    public class TcpProxy
    {
        private readonly int _listenPort;
        private readonly ILoadBalancer _balancer;

        private const int MaxConnectAttempts = 3;
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);

        public TcpProxy(int listenPort, ILoadBalancer balancer)
        {
            _listenPort = listenPort;
            _balancer = balancer;
        }

        public async Task StartAsync()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, _listenPort);

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

                    NetworkStream clientStream = client.GetStream();

                    RequestReader reader = new RequestReader(clientStream);

                    ReadOnlyMemory<byte>? head = await reader.ReadHeadAsync();
                    if (head == null)
                    {
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

                        Task requestTask = ForwardRequestsAsync(reader, head.Value, backendStream);
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

        private async Task ForwardRequestsAsync(RequestReader reader, ReadOnlyMemory<byte> firstHead, Stream backendStream)
        {
            ReadOnlyMemory<byte>? head = firstHead;

            while (head != null)
            {
                HttpRequest request = HttpParser.Parse(Encoding.ASCII.GetString(head.Value.Span));

                Console.WriteLine($"Method: {request.Method}, Path: {request.Path}, Version: {request.Version}");

                long bodyLength = HttpParser.GetBodyLength(request);
                await backendStream.WriteAsync(head.Value);
                await reader.CopyBodyAsync(bodyLength, backendStream);

                head = await reader.ReadHeadAsync();
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