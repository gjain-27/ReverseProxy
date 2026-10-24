using System.Text;
using System.Threading.Channels;
using Proxy.Caching;

namespace Proxy.Tests
{
    public class ResponseForwarderTests
    {
        private const string ContentLengthHead = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\n";
        private const string ContentLengthBody = "hello";
        private const string ChunkedHead = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n";
        private const string ChunkedBody = "5\r\nhello\r\n6\r\n world\r\n0\r\n\r\n";
        private const string Key = "example.com/";

        private static string WithHeaders(string head, params string[] lines)
        {
            return head.Substring(0, head.Length - 2) + string.Concat(lines.Select(line => line + "\r\n")) + "\r\n";
        }

        private static string Miss(string head, string body = "")
        {
            return WithHeaders(head, "X-Cache: MISS") + body;
        }

        private static async Task<string> ForwardAsync(string backend, int chunkSize, LruCache cache, TimeProvider time, params ExpectedResponse[] items)
        {
            var backendReader = new HttpReader(new TrickleStream(Encoding.ASCII.GetBytes(backend), chunkSize));
            Channel<ExpectedResponse> expected = Channel.CreateUnbounded<ExpectedResponse>();
            foreach (ExpectedResponse item in items)
            {
                expected.Writer.TryWrite(item);
            }
            expected.Writer.TryComplete();

            var client = new MemoryStream();
            await ResponseForwarder.ForwardAsync(backendReader, expected.Reader, client, cache, time);

            return Encoding.ASCII.GetString(client.ToArray());
        }

        private static Task<string> ForwardAsync(string backend, int chunkSize, params string[] methods)
        {
            var time = new ManualTimeProvider();
            var items = methods.Select(method => new ExpectedResponse(method)).ToArray();
            return ForwardAsync(backend, chunkSize, new LruCache(1024 * 1024, time), time, items);
        }

        private static Task<string> ForwardAsync(string backend, params string[] methods)
        {
            return ForwardAsync(backend, int.MaxValue, methods);
        }

        [Fact]
        public async Task ForwardAsync_ContentLength_ForwardedExactly()
        {
            Assert.Equal(Miss(ContentLengthHead, ContentLengthBody), await ForwardAsync(ContentLengthHead + ContentLengthBody, "GET"));
        }

        [Fact]
        public async Task ForwardAsync_Chunked_ForwardedExactly()
        {
            Assert.Equal(Miss(ChunkedHead, ChunkedBody), await ForwardAsync(ChunkedHead + ChunkedBody, "GET"));
        }

        [Fact]
        public async Task ForwardAsync_TwoResponses_BothForwardedInOrder()
        {
            string backend = ContentLengthHead + ContentLengthBody + ChunkedHead + ChunkedBody;
            string expected = Miss(ContentLengthHead, ContentLengthBody) + Miss(ChunkedHead, ChunkedBody);

            Assert.Equal(expected, await ForwardAsync(backend, "GET", "GET"));
        }

        [Fact]
        public async Task ForwardAsync_HeadRequest_NoBodyReadAndNextResponseIntact()
        {
            string backend = ContentLengthHead + ContentLengthHead + ContentLengthBody;
            string expected = Miss(ContentLengthHead) + Miss(ContentLengthHead, ContentLengthBody);

            Assert.Equal(expected, await ForwardAsync(backend, "HEAD", "GET"));
        }

        [Fact]
        public async Task ForwardAsync_NoContent_FollowedByAnotherResponse()
        {
            string noContent = "HTTP/1.1 204 No Content\r\n\r\n";
            string backend = noContent + ContentLengthHead + ContentLengthBody;
            string expected = Miss(noContent) + Miss(ContentLengthHead, ContentLengthBody);

            Assert.Equal(expected, await ForwardAsync(backend, "DELETE", "GET"));
        }

        [Fact]
        public async Task ForwardAsync_ContinueThenOk_BothForwardedForOneRequest()
        {
            string interim = "HTTP/1.1 100 Continue\r\n\r\n";
            string backend = interim + ContentLengthHead + ContentLengthBody;
            string expected = Miss(interim) + Miss(ContentLengthHead, ContentLengthBody);

            Assert.Equal(expected, await ForwardAsync(backend, "POST"));
        }

        [Fact]
        public async Task ForwardAsync_UntilClose_ForwardsEverythingToEnd()
        {
            string head = "HTTP/1.1 200 OK\r\nConnection: close\r\n\r\n";
            string body = new string('x', 20000);

            Assert.Equal(Miss(head, body), await ForwardAsync(head + body, "GET"));
        }

        [Fact]
        public async Task ForwardAsync_BackendClosesBeforeResponding_ThrowsIOException()
        {
            await Assert.ThrowsAsync<IOException>(() => ForwardAsync("", "GET"));
        }

        [Fact]
        public async Task ForwardAsync_TwoResponsesInSmallReads_BothForwardedInOrder()
        {
            string backend = ContentLengthHead + ContentLengthBody + ChunkedHead + ChunkedBody;
            string expected = Miss(ContentLengthHead, ContentLengthBody) + Miss(ChunkedHead, ChunkedBody);

            Assert.Equal(expected, await ForwardAsync(backend, 3, "GET", "GET"));
        }

        [Fact]
        public async Task ForwardAsync_CacheableResponse_IsStored()
        {
            var time = new ManualTimeProvider();
            var cache = new LruCache(1024 * 1024, time);
            string head = "HTTP/1.1 200 OK\r\nCache-Control: max-age=60\r\nContent-Length: 5\r\n\r\n";

            string output = await ForwardAsync(head + ContentLengthBody, int.MaxValue, cache, time, new ExpectedResponse("GET", Key));

            Assert.Equal(Miss(head, ContentLengthBody), output);
            Assert.True(cache.TryGet(Key, out CachedResponse? cached));
            Assert.Equal(head, cached!.HeadText);
            Assert.Equal(ContentLengthBody, Encoding.ASCII.GetString(cached.Body));
            Assert.Equal(TimeSpan.FromSeconds(60), cached.Ttl);
        }

        [Fact]
        public async Task ForwardAsync_CachedItem_WritesHitWithAgeWithoutReadingBackend()
        {
            var time = new ManualTimeProvider();
            var cache = new LruCache(1024 * 1024, time);
            string head = "HTTP/1.1 200 OK\r\nCache-Control: max-age=60\r\nContent-Length: 5\r\n\r\n";
            var cached = new CachedResponse(head, Encoding.ASCII.GetBytes(ContentLengthBody), time.GetTimestamp(), TimeSpan.FromSeconds(60));
            time.Advance(TimeSpan.FromSeconds(7));

            string output = await ForwardAsync("", int.MaxValue, cache, time, new ExpectedResponse("GET", Key, cached));

            Assert.Equal(WithHeaders(head, "X-Cache: HIT", "Age: 7") + ContentLengthBody, output);
        }

        [Fact]
        public async Task ForwardAsync_NoStoreResponse_IsNotStored()
        {
            var time = new ManualTimeProvider();
            var cache = new LruCache(1024 * 1024, time);
            string head = "HTTP/1.1 200 OK\r\nCache-Control: no-store\r\nContent-Length: 5\r\n\r\n";

            string output = await ForwardAsync(head + ContentLengthBody, int.MaxValue, cache, time, new ExpectedResponse("GET", Key));

            Assert.Equal(Miss(head, ContentLengthBody), output);
            Assert.False(cache.TryGet(Key, out _));
            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public async Task ForwardAsync_ChunkedCacheableResponse_ReplaysExactBody()
        {
            var time = new ManualTimeProvider();
            var cache = new LruCache(1024 * 1024, time);
            string head = "HTTP/1.1 200 OK\r\nCache-Control: max-age=60\r\nTransfer-Encoding: chunked\r\n\r\n";

            await ForwardAsync(head + ChunkedBody, 3, cache, time, new ExpectedResponse("GET", Key));

            Assert.True(cache.TryGet(Key, out CachedResponse? cached));
            Assert.Equal(ChunkedBody, Encoding.ASCII.GetString(cached!.Body));

            string replay = await ForwardAsync("", int.MaxValue, cache, time, new ExpectedResponse("GET", Key, cached));

            Assert.Equal(WithHeaders(head, "X-Cache: HIT", "Age: 0") + ChunkedBody, replay);
        }
    }
}
