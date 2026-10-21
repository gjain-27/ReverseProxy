namespace Proxy.LoadBalancing
{
    public class Backend
    {
        public string Host { get; }
        public int Port { get; }
        private int _activeConnections;

        public Backend(string host, int port)
        {
            Host = host;
            Port = port;
        }

        public int ActiveConnections => Volatile.Read(ref _activeConnections);

        public void OnConnectionOpened() => Interlocked.Increment(ref _activeConnections);
        public void OnConnectionClosed() => Interlocked.Decrement(ref _activeConnections);

        public override string ToString() => $"{Host}:{Port}";
    }
}
