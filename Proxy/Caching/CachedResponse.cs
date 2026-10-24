namespace Proxy.Caching
{
    public class CachedResponse
    {
        public string HeadText { get; }
        public byte[] Body { get; }
        public long StoredAt { get; }
        public TimeSpan Ttl { get; }

        public CachedResponse(string headText, byte[] body, long storedAt, TimeSpan ttl)
        {
            HeadText = headText;
            Body = body;
            StoredAt = storedAt;
            Ttl = ttl;
        }

        public long SizeBytes => (long)HeadText.Length * sizeof(char) + Body.Length;
    }
}
