namespace Proxy.LoadBalancing
{
    public class Backend
    {
        public string Host { get; }
        public int Port { get; }

        public Backend(string host, int port)
        {
            Host = host;
            Port = port;
        }

        public override string ToString() => $"{Host}:{Port}";
    }
}
