using Proxy.LoadBalancing;
using Proxy.RateLimiting;

namespace Proxy
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            List<Backend> backends = [new Backend("localhost", 5001), new Backend("localhost", 5002), new Backend("localhost", 5003)];
            LeastConnectionsBalancer balancer = new LeastConnectionsBalancer(backends);
            RateLimiter rateLimiter = new RateLimiter(10, 5, TimeProvider.System);
            TcpProxy myProxy = new TcpProxy(8080, balancer, rateLimiter);

            HealthChecker checker = new HealthChecker(backends, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2));
            _ = checker.RunAsync();
            _ = rateLimiter.RunCleanupAsync(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));

            await myProxy.StartAsync();
        }
    }
}