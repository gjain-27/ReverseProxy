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

        [Fact]
        public void GetBodyLength_NoBodyHeaders_ReturnsZero()
        {
            var request = HttpParser.Parse("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n");

            Assert.Equal(0, HttpParser.GetBodyLength(request));
        }

        [Fact]
        public void GetBodyLength_ContentLength_ReturnsValue()
        {
            var request = HttpParser.Parse("POST / HTTP/1.1\r\nContent-Length: 42\r\n\r\n");

            Assert.Equal(42, HttpParser.GetBodyLength(request));
        }

        [Fact]
        public void GetBodyLength_ContentLengthZero_ReturnsZero()
        {
            var request = HttpParser.Parse("POST / HTTP/1.1\r\nContent-Length: 0\r\n\r\n");

            Assert.Equal(0, HttpParser.GetBodyLength(request));
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("-5")]
        [InlineData("+5")]
        [InlineData("1.5")]
        [InlineData("1,000")]
        public void GetBodyLength_InvalidContentLength_Throws(string value)
        {
            var request = HttpParser.Parse($"POST / HTTP/1.1\r\nContent-Length: {value}\r\n\r\n");

            Assert.Throws<FormatException>(() => HttpParser.GetBodyLength(request));
        }

        [Fact]
        public void GetBodyLength_BothBodyHeaders_Throws()
        {
            var request = HttpParser.Parse("POST / HTTP/1.1\r\nContent-Length: 5\r\nTransfer-Encoding: chunked\r\n\r\n");

            Assert.Throws<FormatException>(() => HttpParser.GetBodyLength(request));
        }

        [Fact]
        public void GetBodyLength_TransferEncodingOnly_ThrowsNotSupported()
        {
            var request = HttpParser.Parse("POST / HTTP/1.1\r\nTransfer-Encoding: chunked\r\n\r\n");

            Assert.Throws<NotSupportedException>(() => HttpParser.GetBodyLength(request));
        }

        [Fact]
        public void ParseResponse_SimpleOk_ReadsStatusLine()
        {
            var response = HttpParser.ParseResponse("HTTP/1.1 200 OK");

            Assert.Equal("HTTP/1.1", response.Version);
            Assert.Equal(200, response.StatusCode);
            Assert.Equal("OK", response.Reason);
        }

        [Fact]
        public void ParseResponse_MultiWordReason_KeepsWholeReason()
        {
            var response = HttpParser.ParseResponse("HTTP/1.1 503 Service Unavailable");

            Assert.Equal(503, response.StatusCode);
            Assert.Equal("Service Unavailable", response.Reason);
        }

        [Fact]
        public void ParseResponse_NoReason_ReasonIsEmpty()
        {
            var response = HttpParser.ParseResponse("HTTP/1.1 204");

            Assert.Equal(204, response.StatusCode);
            Assert.Equal("", response.Reason);
        }

        [Fact]
        public void ParseResponse_Headers_AreParsedCaseInsensitively()
        {
            var response = HttpParser.ParseResponse("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 5\r\n\r\n");

            Assert.Equal(2, response.Headers.Count);
            Assert.Equal("text/plain", response.Headers["content-type"]);
            Assert.Equal("5", response.Headers["CONTENT-LENGTH"]);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("99")]
        [InlineData("1000")]
        [InlineData("600")]
        [InlineData("-12")]
        [InlineData("+20")]
        public void ParseResponse_InvalidStatusCode_Throws(string code)
        {
            Assert.Throws<FormatException>(() => HttpParser.ParseResponse($"HTTP/1.1 {code} OK"));
        }

        [Fact]
        public void ParseResponse_InvalidVersion_Throws()
        {
            Assert.Throws<FormatException>(() => HttpParser.ParseResponse("HTTPS/1.1 200 OK"));
        }

        [Fact]
        public void ParseResponse_ConflictingContentLength_Throws()
        {
            Assert.Throws<FormatException>(() => HttpParser.ParseResponse("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Length: 6\r\n\r\n"));
        }

        [Fact]
        public void GetResponseBody_HeadRequest_HasNoBody()
        {
            var response = HttpParser.ParseResponse("HTTP/1.1 200 OK\r\nContent-Length: 100\r\n\r\n");

            Assert.Equal((BodyKind.None, 0L), HttpParser.GetResponseBody("HEAD", response));
        }

        [Theory]
        [InlineData(101)]
        [InlineData(204)]
        [InlineData(304)]
        public void GetResponseBody_NoBodyStatus_HasNoBody(int status)
        {
            var response = HttpParser.ParseResponse($"HTTP/1.1 {status} Status\r\nContent-Length: 10\r\n\r\n");

            Assert.Equal((BodyKind.None, 0L), HttpParser.GetResponseBody("GET", response));
        }

        [Fact]
        public void GetResponseBody_Chunked_ReturnsChunked()
        {
            var response = HttpParser.ParseResponse("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n");

            Assert.Equal((BodyKind.Chunked, 0L), HttpParser.GetResponseBody("GET", response));
        }

        [Fact]
        public void GetResponseBody_ChunkedAndContentLength_ChunkedWins()
        {
            var response = HttpParser.ParseResponse("HTTP/1.1 200 OK\r\nContent-Length: 42\r\nTransfer-Encoding: chunked\r\n\r\n");

            Assert.Equal((BodyKind.Chunked, 0L), HttpParser.GetResponseBody("GET", response));
        }

        [Fact]
        public void GetResponseBody_ContentLength_ReturnsLength()
        {
            var response = HttpParser.ParseResponse("HTTP/1.1 200 OK\r\nContent-Length: 42\r\n\r\n");

            Assert.Equal((BodyKind.ContentLength, 42L), HttpParser.GetResponseBody("GET", response));
        }

        [Fact]
        public void GetResponseBody_NoLengthHeaders_UntilClose()
        {
            var response = HttpParser.ParseResponse("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\n\r\n");

            Assert.Equal((BodyKind.UntilClose, 0L), HttpParser.GetResponseBody("GET", response));
        }

        [Fact]
        public void GetResponseBody_InvalidContentLength_Throws()
        {
            var response = HttpParser.ParseResponse("HTTP/1.1 200 OK\r\nContent-Length: abc\r\n\r\n");

            Assert.Throws<FormatException>(() => HttpParser.GetResponseBody("GET", response));
        }
    }
}
