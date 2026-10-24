using Proxy.Caching;
using Proxy.LoadBalancing;
using Proxy.RateLimiting;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;

namespace Proxy
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            string? envDir = EnvFile.Load();

            bool logRequests = ReadBool("PROXY_LOG_REQUESTS", true);
            int rateLimitCapacity = ReadPositiveInt("PROXY_RATE_LIMIT_CAPACITY", 10);
            double rateLimitPerSecond = ReadPositiveDouble("PROXY_RATE_LIMIT_PER_SECOND", 5);
            int cacheMegabytes = ReadPositiveInt("PROXY_CACHE_MB", 10);

            Log.RequestsEnabled = logRequests;
            Log.Info($"Settings: log requests {logRequests}, rate limit capacity {rateLimitCapacity}, rate limit per second {rateLimitPerSecond.ToString(CultureInfo.InvariantCulture)}, cache {cacheMegabytes} MB");

            string? certPath = Environment.GetEnvironmentVariable("PROXY_CERT_PATH");
            string? certPassword = Environment.GetEnvironmentVariable("PROXY_CERT_PASSWORD");
            if (certPath != null && !Path.IsPathRooted(certPath) && envDir != null)
            {
                certPath = Path.Combine(envDir, certPath);
            }

            List<Backend> backends = [new Backend("localhost", 5001), new Backend("localhost", 5002), new Backend("localhost", 5003)];
            LeastConnectionsBalancer balancer = new LeastConnectionsBalancer(backends);
            RateLimiter rateLimiter = new RateLimiter(rateLimitCapacity, rateLimitPerSecond, TimeProvider.System);
            LruCache cache = new LruCache((long)cacheMegabytes * 1024 * 1024, TimeProvider.System);
            TcpProxy httpProxy = new TcpProxy(8080, balancer, rateLimiter, cache);

            HealthChecker checker = new HealthChecker(backends, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2));
            _ = checker.RunAsync();
            _ = rateLimiter.RunCleanupAsync(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));

            if (!string.IsNullOrEmpty(certPath) && !string.IsNullOrEmpty(certPassword) && File.Exists(certPath))
            {
                X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12FromFile(certPath, certPassword);
                TcpProxy httpsProxy = new TcpProxy(8443, balancer, rateLimiter, cache, certificate);

                await Task.WhenAll(httpProxy.StartAsync(), httpsProxy.StartAsync());
            }
            else
            {
                Log.Info("HTTPS disabled: set PROXY_CERT_PATH and PROXY_CERT_PASSWORD in .env");
                await httpProxy.StartAsync();
            }
        }

        private static bool ReadBool(string name, bool fallback)
        {
            return bool.TryParse(Environment.GetEnvironmentVariable(name), out bool value) ? value : fallback;
        }

        private static int ReadPositiveInt(string name, int fallback)
        {
            return int.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0 ? value : fallback;
        }

        private static double ReadPositiveDouble(string name, double fallback)
        {
            return double.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value) && value > 0 ? value : fallback;
        }
    }
}
