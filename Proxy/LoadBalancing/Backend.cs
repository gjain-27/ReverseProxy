namespace Proxy.LoadBalancing
{
    public class Backend
    {
        public string Host { get; }
        public int Port { get; }
        private int _activeConnections;
        private bool _isHealthy = true;

        public Backend(string host, int port)
        {
            Host = host;
            Port = port;
        }

        public int ActiveConnections => Volatile.Read(ref _activeConnections);
        public bool IsHealthy => Volatile.Read(ref _isHealthy);

        public void OnConnectionOpened() => Interlocked.Increment(ref _activeConnections);
        public void OnConnectionClosed() => Interlocked.Decrement(ref _activeConnections);
        public void MarkHealthy()
        {
            bool wasHealthy = Interlocked.Exchange(ref _isHealthy, true);
            if (!wasHealthy)
            {
                Console.WriteLine($"{this} is UP");
            }
        }
        public void MarkUnhealthy()
        {
            bool wasHealthy = Interlocked.Exchange(ref _isHealthy, false);
            if (wasHealthy)
            {
                Console.WriteLine($"{this} is DOWN");
            }
        }

        public override string ToString() => $"{Host}:{Port}";
    }
}
