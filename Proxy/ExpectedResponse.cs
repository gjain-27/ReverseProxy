using Proxy.Caching;

namespace Proxy
{
    public sealed class ExpectedResponse
    {
        public string Method { get; }
        public string? CacheKey { get; }
        public CachedResponse? Cached { get; }

        public ExpectedResponse(string method, string? cacheKey = null, CachedResponse? cached = null)
        {
            Method = method;
            CacheKey = cacheKey;
            Cached = cached;
        }
    }
}
