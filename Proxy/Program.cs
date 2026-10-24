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
            int httpPort = ReadPort("PROXY_HTTP_PORT", 8080);
            int httpsPort = ReadPort("PROXY_HTTPS_PORT", 8443);
            string balancerName = Environment.GetEnvironmentVariable("PROXY_BALANCER")?.Trim().ToLowerInvariant() == "round-robin" ? "round-robin" : "least-connections";

            List<Backend> backends = ParseBackends(Environment.GetEnvironmentVariable("PROXY_BACKENDS") ?? "localhost:5001,localhost:5002,localhost:5003");
            if (backends.Count == 0)
            {
                Log.Info("Error: PROXY_BACKENDS contains no valid host:port entries");
                Environment.ExitCode = 1;
                return;
            }

            Log.RequestsEnabled = logRequests;
            Log.Info($"Settings: log requests {logRequests}, rate limit capacity {rateLimitCapacity}, rate limit per second {rateLimitPerSecond.ToString(CultureInfo.InvariantCulture)}, cache {cacheMegabytes} MB, backends {string.Join(", ", backends)}, HTTP port {httpPort}, HTTPS port {httpsPort}, balancer {balancerName}");

            string? certPath = Environment.GetEnvironmentVariable("PROXY_CERT_PATH");
            string? certPassword = Environment.GetEnvironmentVariable("PROXY_CERT_PASSWORD");
            if (certPath != null && !Path.IsPathRooted(certPath) && envDir != null)
            {
                certPath = Path.Combine(envDir, certPath);
            }

            ILoadBalancer balancer = balancerName == "round-robin" ? new RoundRobinBalancer(backends) : new LeastConnectionsBalancer(backends);
            RateLimiter rateLimiter = new RateLimiter(rateLimitCapacity, rateLimitPerSecond, TimeProvider.System);
            LruCache cache = new LruCache((long)cacheMegabytes * 1024 * 1024, TimeProvider.System);
            TcpProxy httpProxy = new TcpProxy(httpPort, balancer, rateLimiter, cache);

            HealthChecker checker = new HealthChecker(backends, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2));
            _ = checker.RunAsync();
            _ = rateLimiter.RunCleanupAsync(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));

            if (!string.IsNullOrEmpty(certPath) && !string.IsNullOrEmpty(certPassword) && File.Exists(certPath))
            {
                X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12FromFile(certPath, certPassword);
                TcpProxy httpsProxy = new TcpProxy(httpsPort, balancer, rateLimiter, cache, certificate);

                await Task.WhenAll(httpProxy.StartAsync(), httpsProxy.StartAsync());
            }
            else
            {
                Log.Info("HTTPS disabled: set PROXY_CERT_PATH and PROXY_CERT_PASSWORD in .env");
                await httpProxy.StartAsync();
            }
        }

        private static List<Backend> ParseBackends(string value)
        {
            List<Backend> backends = new List<Backend>();

            foreach (string rawEntry in value.Split(','))
            {
                string entry = rawEntry.Trim();
                if (entry.Length == 0)
                {
                    continue;
                }

                int colon = entry.LastIndexOf(':');
                if (colon <= 0
                    || !int.TryParse(entry.Substring(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int port)
                    || port < 1 || port > 65535)
                {
                    Log.Info($"Skipping invalid backend '{entry}'");
                    continue;
                }

                backends.Add(new Backend(entry.Substring(0, colon), port));
            }

            return backends;
        }

        private static int ReadPort(string name, int fallback)
        {
            int port = ReadPositiveInt(name, fallback);
            return port <= 65535 ? port : fallback;
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
