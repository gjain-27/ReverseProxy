using System.Globalization;

namespace Proxy.Caching
{
    public static class CachePolicy
    {
        public const int MaxEntryBytes = 1024 * 1024;

        public static string? GetCacheKey(HttpRequest request)
        {
            if (!request.Method.Equals("GET", StringComparison.Ordinal))
            {
                return null;
            }

            if (request.Headers.ContainsKey("Authorization"))
            {
                return null;
            }

            if (!request.Headers.TryGetValue("Host", out string? host))
            {
                return null;
            }

            return host.ToLowerInvariant() + request.Path;
        }

        public static TimeSpan? GetTimeToLive(HttpResponse response)
        {
            if (response.StatusCode != 200
                || response.Headers.ContainsKey("Set-Cookie")
                || response.Headers.ContainsKey("Vary")
                || !response.Headers.TryGetValue("Cache-Control", out string? cacheControl))
            {
                return null;
            }

            long? maxAge = null;

            foreach (string rawDirective in cacheControl.Split(','))
            {
                string directive = rawDirective.Trim();

                if (directive.Equals("no-store", StringComparison.OrdinalIgnoreCase)
                    || directive.Equals("private", StringComparison.OrdinalIgnoreCase)
                    || directive.Equals("no-cache", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                int equals = directive.IndexOf('=');
                if (maxAge == null
                    && equals > 0
                    && directive.Substring(0, equals).Trim().Equals("max-age", StringComparison.OrdinalIgnoreCase))
                {
                    string value = directive.Substring(equals + 1).Trim();
                    if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long seconds) || seconds <= 0)
                    {
                        return null;
                    }

                    maxAge = seconds;
                }
            }

            if (maxAge == null)
            {
                return null;
            }

            return TimeSpan.FromSeconds(maxAge.Value);
        }
    }
}
