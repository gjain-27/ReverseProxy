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

        private const int MaxHeadSize = 8192;
        private static readonly TimeSpan HeadTimeout = TimeSpan.FromSeconds(10);

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

                    RequestHead? head = await ReadHeadAsync(clientStream);
                    if (head == null)
                    {
                        return;
                    }

                    HttpRequest httpRequest = HttpParser.Parse(Encoding.ASCII.GetString(head.Buffer, 0, head.HeadLength));

                    Console.WriteLine($"Method: {httpRequest.Method}, Path: {httpRequest.Path}");

                    using TcpClient backend = new TcpClient();
                    await backend.ConnectAsync(_backendHost, _backendPort);

                    NetworkStream backendStream = backend.GetStream();
                    await backendStream.WriteAsync(head.Buffer.AsMemory(0, head.TotalRead));

                    Task clientToBackendTask = clientStream.CopyToAsync(backendStream);
                    Task backendToClientTask = backendStream.CopyToAsync(clientStream);

                    await Task.WhenAny(clientToBackendTask, backendToClientTask);

                    Console.WriteLine("Connection closed");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Connection error: {e.Message}");
                }
            }
        }

        private async Task<RequestHead?> ReadHeadAsync(NetworkStream stream)
        {
            byte[] buffer = new byte[MaxHeadSize];
            int totalRead = 0;

            using CancellationTokenSource timeout = new CancellationTokenSource(HeadTimeout);

            try
            {
                while (true)
                {
                    if (totalRead >= MaxHeadSize)
                    {
                        throw new InvalidDataException($"Request head exceeds {MaxHeadSize} bytes");
                    }

                    int bytesRead = await stream.ReadAsync(buffer.AsMemory(totalRead), timeout.Token);
                    if (bytesRead == 0)
                    {
                        return null;
                    }

                    int searchStart = Math.Max(0, totalRead - 3);
                    totalRead += bytesRead;

                    int index = buffer.AsSpan(searchStart, totalRead - searchStart).IndexOf("\r\n\r\n"u8);
                    if (index >= 0)
                    {
                        return new RequestHead(buffer, searchStart + index + 4, totalRead);
                    }
                }
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                throw new TimeoutException($"Timed out after {HeadTimeout.TotalSeconds}s waiting for request head");
            }
        }
    }
}