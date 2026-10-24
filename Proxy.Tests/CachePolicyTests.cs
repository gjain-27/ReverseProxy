using Proxy.Caching;

namespace Proxy.Tests
{
    public class CachePolicyTests
    {
        private static TimeSpan? TtlOf(string head)
        {
            return CachePolicy.GetTimeToLive(HttpParser.ParseResponse(head + "\r\n\r\n"));
        }

        [Fact]
        public void GetCacheKey_GetWithHost_ReturnsLowerCasedHostPlusPath()
        {
            var request = HttpParser.Parse("GET /Index.html HTTP/1.1\r\nHost: Example.COM\r\n\r\n");

            Assert.Equal("example.com/Index.html", CachePolicy.GetCacheKey(request));
        }

        [Fact]
        public void GetCacheKey_Post_ReturnsNull()
        {
            var request = HttpParser.Parse("POST / HTTP/1.1\r\nHost: example.com\r\n\r\n");

            Assert.Null(CachePolicy.GetCacheKey(request));
        }

        [Fact]
        public void GetCacheKey_Authorization_ReturnsNull()
        {
            var request = HttpParser.Parse("GET / HTTP/1.1\r\nHost: example.com\r\nAuthorization: Bearer abc\r\n\r\n");

            Assert.Null(CachePolicy.GetCacheKey(request));
        }

        [Fact]
        public void GetCacheKey_NoHost_ReturnsNull()
        {
            var request = HttpParser.Parse("GET / HTTP/1.1\r\n\r\n");

            Assert.Null(CachePolicy.GetCacheKey(request));
        }

        [Fact]
        public void GetTimeToLive_MaxAge_ReturnsSeconds()
        {
            Assert.Equal(TimeSpan.FromSeconds(10), TtlOf("HTTP/1.1 200 OK\r\nCache-Control: max-age=10"));
        }

        [Fact]
        public void GetTimeToLive_NoCacheControl_ReturnsNull()
        {
            Assert.Null(TtlOf("HTTP/1.1 200 OK\r\nContent-Length: 5"));
        }

        [Theory]
        [InlineData("no-store")]
        [InlineData("private")]
        [InlineData("no-cache")]
        public void GetTimeToLive_ForbiddingDirective_ReturnsNull(string directive)
        {
            Assert.Null(TtlOf($"HTTP/1.1 200 OK\r\nCache-Control: max-age=60, {directive}"));
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-5")]
        [InlineData("abc")]
        public void GetTimeToLive_InvalidMaxAge_ReturnsNull(string value)
        {
            Assert.Null(TtlOf($"HTTP/1.1 200 OK\r\nCache-Control: max-age={value}"));
        }

        [Fact]
        public void GetTimeToLive_NonOkStatus_ReturnsNull()
        {
            Assert.Null(TtlOf("HTTP/1.1 404 Not Found\r\nCache-Control: max-age=10"));
        }

        [Fact]
        public void GetTimeToLive_SetCookie_ReturnsNull()
        {
            Assert.Null(TtlOf("HTTP/1.1 200 OK\r\nCache-Control: max-age=10\r\nSet-Cookie: id=1"));
        }

        [Fact]
        public void GetTimeToLive_Vary_ReturnsNull()
        {
            Assert.Null(TtlOf("HTTP/1.1 200 OK\r\nCache-Control: max-age=10\r\nVary: Accept-Encoding"));
        }

        [Fact]
        public void GetTimeToLive_PublicWithMaxAge_ReturnsSeconds()
        {
            Assert.Equal(TimeSpan.FromSeconds(30), TtlOf("HTTP/1.1 200 OK\r\nCache-Control: public, max-age=30"));
        }
    }
}
