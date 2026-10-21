using Proxy.LoadBalancing;

namespace Proxy
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            List<Backend> backends = [new Backend("localhost", 5001), new Backend("localhost", 5002), new Backend("localhost", 5003)];
            RoundRobinBalancer balancer = new RoundRobinBalancer(backends);
            TcpProxy myProxy = new TcpProxy(8080, balancer);

            await myProxy.StartAsync();
        }
    }
}