namespace Proxy.Tests
{
    public class HttpParserTests
    {
        [Fact]
        public void Parse_SimpleGet_ReadsRequestLine()
        {
            var request = HttpParser.Parse("GET /hello HTTP/1.1");

            Assert.Equal("GET", request.Method);
            Assert.Equal("/hello", request.Path);
            Assert.Equal("HTTP/1.1", request.Version);
        }

        [Fact]
        public void Parse_MultipleHeaders_ReadsAll()
        {
            var request = HttpParser.Parse("GET / HTTP/1.1\r\nHost: example.com\r\nAccept: text/html\r\nUser-Agent: test\r\n\r\n");

            Assert.Equal(3, request.Headers.Count);
            Assert.Equal("example.com", request.Headers["Host"]);
            Assert.Equal("text/html", request.Headers["Accept"]);
            Assert.Equal("test", request.Headers["User-Agent"]);
        }

        [Fact]
        public void Parse_HeaderLookup_IsCaseInsensitive()
        {
            var request = HttpParser.Parse("GET / HTTP/1.1\r\nContent-Type: text/plain\r\n\r\n");

            Assert.Equal("text/plain", request.Headers["content-type"]);
            Assert.Equal("text/plain", request.Headers["CONTENT-TYPE"]);
        }

        [Fact]
        public void Parse_ValueContainingColon_KeepsWholeValue()
        {
            var request = HttpParser.Parse("GET / HTTP/1.1\r\nHost: localhost:8080\r\n\r\n");

            Assert.Equal("localhost:8080", request.Headers["Host"]);
        }

        [Fact]
        public void Parse_WhitespaceAroundValue_IsTrimmed()
        {
            var request = HttpParser.Parse("GET / HTTP/1.1\r\nHost:   example.com   \r\n\r\n");

            Assert.Equal("example.com", request.Headers["Host"]);
        }

        [Theory]
        [InlineData("GET /")]
        [InlineData("GET  / HTTP/1.1")]
        public void Parse_MalformedRequestLine_Throws(string head)
        {
            Assert.Throws<FormatException>(() => HttpParser.Parse(head));
        }

        [Fact]
        public void Parse_InvalidVersion_Throws()
        {
            Assert.Throws<FormatException>(() => HttpParser.Parse("GET / banana"));
        }

        [Theory]
        [InlineData("GET / HTTP/1.1\r\nHost example.com\r\n\r\n")]
        [InlineData("GET / HTTP/1.1\r\n: value\r\n\r\n")]
        public void Parse_MalformedHeaderLine_Throws(string head)
        {
            Assert.Throws<FormatException>(() => HttpParser.Parse(head));
        }

        [Fact]
        public void Parse_WhitespaceInHeaderName_Throws()
        {
            Assert.Throws<FormatException>(() =>
                HttpParser.Parse("GET / HTTP/1.1\r\nContent-Length : 5\r\n\r\n"));
        }

        [Fact]
        public void Parse_DuplicateContentLengthDifferentValues_Throws()
        {
            Assert.Throws<FormatException>(() =>
                HttpParser.Parse("POST / HTTP/1.1\r\nContent-Length: 5\r\nContent-Length: 6\r\n\r\n"));
        }

        [Fact]
        public void Parse_DuplicateContentLengthSameValue_IsAllowed()
        {
            var request = HttpParser.Parse("POST / HTTP/1.1\r\nContent-Length: 5\r\nContent-Length: 5\r\n\r\n");

            Assert.Equal("5", request.Headers["Content-Length"]);
        }

        [Fact]
        public void Parse_DuplicateOtherHeader_CombinesValues()
        {
            var request = HttpParser.Parse("GET / HTTP/1.1\r\nAccept: a\r\naccept: b\r\n\r\n");

            Assert.Equal("a, b", request.Headers["Accept"]);
        }

        [Fact]
        public void Parse_BareLineFeedInHeaderLine_Throws()
        {
            Assert.Throws<FormatException>(() => HttpParser.Parse("GET / HTTP/1.1\r\nHost: example.com\nX-Admin: true\r\n\r\n"));
        }
    }
}
