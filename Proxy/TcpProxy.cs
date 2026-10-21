using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Proxy
{
    public class TcpProxy
    {
        private readonly int _listenPort;
        private readonly string _backendHost;
        private readonly int _backendPort;

        public TcpProxy(int listenPort, string backendHost, int backendPort)
        {
            _listenPort = listenPort;
            _backendHost = backendHost;
            _backendPort = backendPort;
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

                    using TcpClient backend = new TcpClient();
                    await backend.ConnectAsync(_backendHost, _backendPort);

                    NetworkStream backendStream = backend.GetStream();

                    Task requestTask = ForwardRequestsAsync(reader, backendStream);
                    Task responseTask = backendStream.CopyToAsync(clientStream);

                    Task finished = await Task.WhenAny(requestTask, responseTask);
                    await finished;

                    Console.WriteLine("Connection closed");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Connection error: {e.Message}");
                }
            }
        }

        private async Task ForwardRequestsAsync(RequestReader reader, Stream backendStream)
        {
            while (true)
            {
                ReadOnlyMemory<byte>? head = await reader.ReadHeadAsync();

                if (head == null)
                {
                    return;
                }

                HttpRequest request = HttpParser.Parse(Encoding.ASCII.GetString(head.Value.Span));

                Console.WriteLine($"Method: {request.Method}, Path: {request.Path}, Version: {request.Version}");

                long bodyLength = HttpParser.GetBodyLength(request);
                await backendStream.WriteAsync(head.Value);
                await reader.CopyBodyAsync(bodyLength, backendStream);
            }
        }
    }
}