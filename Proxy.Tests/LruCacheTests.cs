using Proxy.Caching;

namespace Proxy.Tests
{
    public class LruCacheTests
    {
        private const string Head = "HTTP/1.1 200 OK\r\n\r\n";

        private static CachedResponse Response(ManualTimeProvider time, int totalSize, int ttlSeconds = 60)
        {
            return new CachedResponse(Head, new byte[totalSize - Head.Length * sizeof(char)], time.GetTimestamp(), TimeSpan.FromSeconds(ttlSeconds));
        }

        [Fact]
        public void TryGet_MissingKey_ReturnsFalse()
        {
            var cache = new LruCache(1000, new ManualTimeProvider());

            Assert.False(cache.TryGet("missing", out CachedResponse? response));
            Assert.Null(response);
        }

        [Fact]
        public void TryGet_AfterSet_ReturnsSameResponse()
        {
            var time = new ManualTimeProvider();
            var cache = new LruCache(1000, time);
            CachedResponse stored = Response(time, 100);

            cache.Set("a", stored);

            Assert.True(cache.TryGet("a", out CachedResponse? response));
            Assert.Same(stored, response);
        }

        [Fact]
        public void Set_OverCapacity_EvictsLeastRecentlyUsed()
        {
            var time = new ManualTimeProvider();
            var cache = new LruCache(250, time);

            cache.Set("a", Response(time, 100));
            cache.Set("b", Response(time, 100));
            cache.Set("c", Response(time, 100));

            Assert.False(cache.TryGet("a", out _));
            Assert.True(cache.TryGet("b", out _));
            Assert.True(cache.TryGet("c", out _));
            Assert.Equal(2, cache.Count);
            Assert.Equal(200, cache.SizeBytes);
        }

        [Fact]
        public void TryGet_RefreshesRecency()
        {
            var time = new ManualTimeProvider();
            var cache = new LruCache(250, time);

            cache.Set("a", Response(time, 100));
            cache.Set("b", Response(time, 100));
            Assert.True(cache.TryGet("a", out _));
            cache.Set("c", Response(time, 100));

            Assert.True(cache.TryGet("a", out _));
            Assert.False(cache.TryGet("b", out _));
            Assert.True(cache.TryGet("c", out _));
        }

        [Fact]
        public void TryGet_ExpiredEntry_ReturnsFalseAndRemovesIt()
        {
            var time = new ManualTimeProvider();
            var cache = new LruCache(1000, time);
            cache.Set("a", Response(time, 100, ttlSeconds: 10));

            time.Advance(TimeSpan.FromSeconds(10));

            Assert.False(cache.TryGet("a", out _));
            Assert.Equal(0, cache.Count);
            Assert.Equal(0, cache.SizeBytes);
        }

        [Fact]
        public void Set_SameKey_ReplacesEntryAndUpdatesSize()
        {
            var time = new ManualTimeProvider();
            var cache = new LruCache(1000, time);
            CachedResponse replacement = Response(time, 300);

            cache.Set("a", Response(time, 100));
            cache.Set("a", replacement);

            Assert.Equal(1, cache.Count);
            Assert.Equal(300, cache.SizeBytes);
            Assert.True(cache.TryGet("a", out CachedResponse? response));
            Assert.Same(replacement, response);
        }

        [Fact]
        public void Set_EntryLargerThanCapacity_IsNotStored()
        {
            var time = new ManualTimeProvider();
            var cache = new LruCache(250, time);

            cache.Set("a", Response(time, 300));

            Assert.Equal(0, cache.Count);
            Assert.Equal(0, cache.SizeBytes);
            Assert.False(cache.TryGet("a", out _));
        }
    }
}
