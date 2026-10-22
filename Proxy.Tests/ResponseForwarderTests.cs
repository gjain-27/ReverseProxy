using System.Text;
using System.Threading.Channels;

namespace Proxy.Tests
{
    public class ResponseForwarderTests
    {
        private const string ContentLengthResponse = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello";
        private const string ChunkedResponse = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhello\r\n6\r\n world\r\n0\r\n\r\n";

        private static async Task<string> ForwardAsync(string backend, int chunkSize, params string[] methods)
        {
            var backendReader = new HttpReader(new TrickleStream(Encoding.ASCII.GetBytes(backend), chunkSize));
            Channel<ExpectedResponse> expected = Channel.CreateUnbounded<ExpectedResponse>();
            foreach (string method in methods)
            {
                expected.Writer.TryWrite(new ExpectedResponse(method));
            }
            expected.Writer.TryComplete();

            var client = new MemoryStream();
            await ResponseForwarder.ForwardAsync(backendReader, expected.Reader, client);

            return Encoding.ASCII.GetString(client.ToArray());
        }

        private static Task<string> ForwardAsync(string backend, params string[] methods)
        {
            return ForwardAsync(backend, int.MaxValue, methods);
        }

        [Fact]
        public async Task ForwardAsync_ContentLength_ForwardedExactly()
        {
            Assert.Equal(ContentLengthResponse, await ForwardAsync(ContentLengthResponse, "GET"));
        }

        [Fact]
        public async Task ForwardAsync_Chunked_ForwardedExactly()
        {
            Assert.Equal(ChunkedResponse, await ForwardAsync(ChunkedResponse, "GET"));
        }

        [Fact]
        public async Task ForwardAsync_TwoResponses_BothForwardedInOrder()
        {
            string backend = ContentLengthResponse + ChunkedResponse;

            Assert.Equal(backend, await ForwardAsync(backend, "GET", "GET"));
        }

        [Fact]
        public async Task ForwardAsync_HeadRequest_NoBodyReadAndNextResponseIntact()
        {
            string headResponse = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\n";
            string backend = headResponse + ContentLengthResponse;

            Assert.Equal(backend, await ForwardAsync(backend, "HEAD", "GET"));
        }

        [Fact]
        public async Task ForwardAsync_NoContent_FollowedByAnotherResponse()
        {
            string backend = "HTTP/1.1 204 No Content\r\n\r\n" + ContentLengthResponse;

            Assert.Equal(backend, await ForwardAsync(backend, "DELETE", "GET"));
        }

        [Fact]
        public async Task ForwardAsync_ContinueThenOk_BothForwardedForOneRequest()
        {
            string backend = "HTTP/1.1 100 Continue\r\n\r\n" + ContentLengthResponse;

            Assert.Equal(backend, await ForwardAsync(backend, "POST"));
        }

        [Fact]
        public async Task ForwardAsync_UntilClose_ForwardsEverythingToEnd()
        {
            string backend = "HTTP/1.1 200 OK\r\nConnection: close\r\n\r\n" + new string('x', 20000);

            Assert.Equal(backend, await ForwardAsync(backend, "GET"));
        }

        [Fact]
        public async Task ForwardAsync_BackendClosesBeforeResponding_ThrowsIOException()
        {
            await Assert.ThrowsAsync<IOException>(() => ForwardAsync("", "GET"));
        }

        [Fact]
        public async Task ForwardAsync_TwoResponsesInSmallReads_BothForwardedInOrder()
        {
            string backend = ContentLengthResponse + ChunkedResponse;

            Assert.Equal(backend, await ForwardAsync(backend, 3, "GET", "GET"));
        }
    }
}
