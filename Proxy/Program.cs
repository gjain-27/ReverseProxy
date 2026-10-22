using Proxy.LoadBalancing;
using Proxy.RateLimiting;
using System.Security.Cryptography.X509Certificates;

namespace Proxy
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            string? envDir = EnvFile.Load();

            string? certPath = Environment.GetEnvironmentVariable("PROXY_CERT_PATH");
            string? certPassword = Environment.GetEnvironmentVariable("PROXY_CERT_PASSWORD");
            if (certPath != null && !Path.IsPathRooted(certPath) && envDir != null)
            {
                certPath = Path.Combine(envDir, certPath);
            }

            List<Backend> backends = [new Backend("localhost", 5001), new Backend("localhost", 5002), new Backend("localhost", 5003)];
            LeastConnectionsBalancer balancer = new LeastConnectionsBalancer(backends);
            RateLimiter rateLimiter = new RateLimiter(10, 5, TimeProvider.System);
            TcpProxy httpProxy = new TcpProxy(8080, balancer, rateLimiter);

            HealthChecker checker = new HealthChecker(backends, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2));
            _ = checker.RunAsync();
            _ = rateLimiter.RunCleanupAsync(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));

            if (!string.IsNullOrEmpty(certPath) && !string.IsNullOrEmpty(certPassword) && File.Exists(certPath))
            {
                X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12FromFile(certPath, certPassword);
                TcpProxy httpsProxy = new TcpProxy(8443, balancer, rateLimiter, certificate);

                await Task.WhenAll(httpProxy.StartAsync(), httpsProxy.StartAsync());
            }
            else
            {
                Console.WriteLine("HTTPS disabled: set PROXY_CERT_PATH and PROXY_CERT_PASSWORD in .env");
                await httpProxy.StartAsync();
            }
        }
    }
}
