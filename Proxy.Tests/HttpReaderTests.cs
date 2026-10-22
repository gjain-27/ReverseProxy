using System.Text;

namespace Proxy.Tests
{
    public class RequestReaderTests
    {
        private static MemoryStream StreamOf(string text)
        {
            return new MemoryStream(Encoding.ASCII.GetBytes(text));
        }

        private static string AsText(ReadOnlyMemory<byte>? head)
        {
            Assert.NotNull(head);
            return Encoding.ASCII.GetString(head.Value.Span);
        }

        [Fact]
        public async Task CopyBodyAsync_ZeroLength_WritesNothing()
        {
            var reader = new RequestReader(StreamOf("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n"));
            var destination = new MemoryStream();

            await reader.ReadHeadAsync();
            await reader.CopyBodyAsync(0, destination);

            Assert.Empty(destination.ToArray());
        }

        [Fact]
        public async Task CopyBodyAsync_BodyArrivesWithHead_CopiesExactlyBody()
        {
            var reader = new RequestReader(StreamOf("POST / HTTP/1.1\r\nContent-Length: 5\r\n\r\nhello"));
            var destination = new MemoryStream();

            await reader.ReadHeadAsync();
            await reader.CopyBodyAsync(5, destination);

            Assert.Equal("hello", Encoding.ASCII.GetString(destination.ToArray()));
        }

        [Fact]
        public async Task ReadHeadAsync_AfterBody_ReturnsNextRequestHead()
        {
            string post = "POST /submit HTTP/1.1\r\nContent-Length: 5\r\n\r\n";
            string get = "GET /next HTTP/1.1\r\nHost: example.com\r\n\r\n";
            var reader = new RequestReader(StreamOf(post + "hello" + get));
            var destination = new MemoryStream();

            Assert.Equal(post, AsText(await reader.ReadHeadAsync()));
            await reader.CopyBodyAsync(5, destination);

            Assert.Equal("hello", Encoding.ASCII.GetString(destination.ToArray()));
            Assert.Equal(get, AsText(await reader.ReadHeadAsync()));
        }

        [Fact]
        public async Task CopyBodyAsync_BodyLargerThanBuffer_CopiesAll()
        {
            byte[] head = Encoding.ASCII.GetBytes("POST / HTTP/1.1\r\nContent-Length: 20000\r\n\r\n");
            byte[] body = new byte[20000];
            for (int i = 0; i < body.Length; i++)
            {
                body[i] = (byte)(i % 251);
            }

            var reader = new RequestReader(new MemoryStream([.. head, .. body]));
            var destination = new MemoryStream();

            await reader.ReadHeadAsync();
            await reader.CopyBodyAsync(body.Length, destination);

            Assert.Equal(body, destination.ToArray());
        }

        [Fact]
        public async Task CopyBodyAsync_StreamEndsMidBody_ThrowsIOException()
        {
            var reader = new RequestReader(StreamOf("POST / HTTP/1.1\r\nContent-Length: 10\r\n\r\nhello"));
            var destination = new MemoryStream();

            await reader.ReadHeadAsync();

            await Assert.ThrowsAsync<IOException>(() => reader.CopyBodyAsync(10, destination));
        }

        [Fact]
        public async Task ReadHeadAsync_StreamEndsBetweenRequests_ReturnsNull()
        {
            var reader = new RequestReader(StreamOf("GET / HTTP/1.1\r\nHost: example.com\r\n\r\n"));

            Assert.NotNull(await reader.ReadHeadAsync());
            Assert.Null(await reader.ReadHeadAsync());
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(7)]
        [InlineData(64)]
        public async Task ReadHeadAsync_HeadArrivesInSmallChunks_ReturnsFullHead(int chunkSize)
        {
            string head = "GET /index.html HTTP/1.1\r\nHost: example.com\r\nAccept: text/html\r\n\r\n";
            var reader = new RequestReader(new TrickleStream(Encoding.ASCII.GetBytes(head), chunkSize));

            Assert.Equal(head, AsText(await reader.ReadHeadAsync()));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(7)]
        [InlineData(64)]
        public async Task ReadHeadAsync_RequestsInSmallChunks_ReturnsEachHeadAndBody(int chunkSize)
        {
            string post = "POST /submit HTTP/1.1\r\nContent-Length: 11\r\n\r\n";
            string get = "GET /next HTTP/1.1\r\nHost: example.com\r\n\r\n";
            byte[] input = Encoding.ASCII.GetBytes(post + "hello world" + get);
            var reader = new RequestReader(new TrickleStream(input, chunkSize));
            var destination = new MemoryStream();

            Assert.Equal(post, AsText(await reader.ReadHeadAsync()));
            await reader.CopyBodyAsync(11, destination);

            Assert.Equal("hello world", Encoding.ASCII.GetString(destination.ToArray()));
            Assert.Equal(get, AsText(await reader.ReadHeadAsync()));
            Assert.Null(await reader.ReadHeadAsync());
        }

        [Fact]
        public async Task CopyBodyAsync_LargeBodyInSmallChunks_CopiesAll()
        {
            byte[] head = Encoding.ASCII.GetBytes("POST / HTTP/1.1\r\nContent-Length: 20000\r\n\r\n");
            byte[] body = new byte[20000];
            for (int i = 0; i < body.Length; i++)
            {
                body[i] = (byte)(i % 251);
            }

            var reader = new RequestReader(new TrickleStream([.. head, .. body], 1000));
            var destination = new MemoryStream();

            await reader.ReadHeadAsync();
            await reader.CopyBodyAsync(body.Length, destination);

            Assert.Equal(body, destination.ToArray());
        }

        [Fact]
        public async Task ReadHeadAsync_HeadTooLarge_ThrowsInvalidDataException()
        {
            var reader = new RequestReader(StreamOf($"GET / HTTP/1.1\r\nX-Big: {new string('a', 9000)}\r\n\r\n"));

            await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadHeadAsync());
        }

        [Fact]
        public async Task ReadHeadAsync_StreamEndsMidHead_ThrowsIOException()
        {
            var reader = new RequestReader(StreamOf("GET / HTTP/1.1\r\nHost: exa"));

            await Assert.ThrowsAsync<IOException>(() => reader.ReadHeadAsync());
        }
    }
}
