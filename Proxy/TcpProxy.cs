using System.Net;
using System.Net.Sockets;

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

                    using TcpClient backend = new TcpClient();
                    await backend.ConnectAsync(_backendHost, _backendPort);

                    NetworkStream clientStream = client.GetStream();
                    NetworkStream backendStream = backend.GetStream();

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
    }
}