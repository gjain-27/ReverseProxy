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

                    Backend target = _balancer.Pick();

                    target.OnConnectionOpened();
                    try
                    {
                        Console.WriteLine($"Forwarding to {target}");

                        using TcpClient backend = new TcpClient();
                        await backend.ConnectAsync(target.Host, target.Port);

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
    }
}