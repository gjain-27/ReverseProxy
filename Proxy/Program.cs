using Proxy.LoadBalancing;

namespace Proxy
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            List<Backend> backends = [new Backend("localhost", 5001), new Backend("localhost", 5002), new Backend("localhost", 5003)];
            LeastConnectionsBalancer balancer = new LeastConnectionsBalancer(backends);
            TcpProxy myProxy = new TcpProxy(8080, balancer);

            HealthChecker checker = new HealthChecker(backends, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2));
            _ = checker.RunAsync();

            await myProxy.StartAsync();
        }
    }
}