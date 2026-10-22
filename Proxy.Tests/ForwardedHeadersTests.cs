using System.Net;
using System.Text;

namespace Proxy.Tests
{
    public class ForwardedHeadersTests
    {
        private static HttpRequest RewriteAndParse(string head, string ip, string proto)
        {
            byte[] result = ForwardedHeaders.Rewrite(head, IPAddress.Parse(ip), proto);
            return HttpParser.Parse(Encoding.ASCII.GetString(result));
        }

        [Fact]
        public void Rewrite_AddsForwardedForAndProto()
        {
            var request = RewriteAndParse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n", "203.0.113.7", "https");

            Assert.Equal("203.0.113.7", request.Headers["X-Forwarded-For"]);
            Assert.Equal("https", request.Headers["X-Forwarded-Proto"]);
        }

        [Fact]
        public void Rewrite_KeepsRequestLineAndOtherHeaders()
        {
            var request = RewriteAndParse("POST /submit HTTP/1.1\r\nHost: example.com\r\nContent-Length: 5\r\n\r\n", "10.0.0.1", "http");

            Assert.Equal("POST", request.Method);
            Assert.Equal("/submit", request.Path);
            Assert.Equal("example.com", request.Headers["Host"]);
            Assert.Equal("5", request.Headers["Content-Length"]);
        }

        [Fact]
        public void Rewrite_ReplacesClientSuppliedForwardedHeaders()
        {
            string head = "GET / HTTP/1.1\r\nx-forwarded-for: 1.2.3.4\r\nX-Forwarded-Proto: https\r\n\r\n";

            var request = RewriteAndParse(head, "10.0.0.1", "http");

            Assert.Equal("10.0.0.1", request.Headers["X-Forwarded-For"]);
            Assert.Equal("http", request.Headers["X-Forwarded-Proto"]);
        }

        [Fact]
        public void Rewrite_IPv4MappedAddress_IsWrittenAsIPv4()
        {
            var request = RewriteAndParse("GET / HTTP/1.1\r\n\r\n", "::ffff:127.0.0.1", "http");

            Assert.Equal("127.0.0.1", request.Headers["X-Forwarded-For"]);
        }

        [Fact]
        public void Rewrite_EndsWithBlankLine()
        {
            byte[] result = ForwardedHeaders.Rewrite("GET / HTTP/1.1\r\nHost: a\r\n\r\n", IPAddress.Parse("10.0.0.1"), "http");

            Assert.EndsWith("\r\n\r\n", Encoding.ASCII.GetString(result));
        }
    }
}
