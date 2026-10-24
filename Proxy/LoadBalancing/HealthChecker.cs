using System.Net.Sockets;

namespace Proxy.LoadBalancing
{
    public class HealthChecker
    {
        private readonly IReadOnlyList<Backend> _backends;
        private readonly TimeSpan _interval;
        private readonly TimeSpan _timeout;

        public HealthChecker(IReadOnlyList<Backend> backends, TimeSpan interval, TimeSpan timeout)
        {
            _backends = backends;
            _interval = interval;
            _timeout = timeout;
        }

        public async Task RunAsync()
        {
            while (true)
            {
                try
                {
                    await CheckAllAsync();
                }
                catch (Exception e)
                {
                    Log.Info($"Health check error: {e.Message}");
                }

                await Task.Delay(_interval);
            }
        }

        public Task CheckAllAsync()
        {
            return Task.WhenAll(_backends.Select(CheckAsync));
        }

        private async Task CheckAsync(Backend backend)
        {
            using TcpClient client = new TcpClient();
            using CancellationTokenSource timeout = new CancellationTokenSource(_timeout);

            try
            {
                await client.ConnectAsync(backend.Host, backend.Port, timeout.Token);
                backend.MarkHealthy();
            }
            catch (Exception e) when (e is SocketException || e is OperationCanceledException)
            {
                backend.MarkUnhealthy();
            }
        }
    }
}
