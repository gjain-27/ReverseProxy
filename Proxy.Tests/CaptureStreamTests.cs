using System.Text;
using Proxy.Caching;

namespace Proxy.Tests
{
    public class CaptureStreamTests
    {
        [Fact]
        public async Task Write_UnderLimit_ReachesInnerAndIsCaptured()
        {
            var inner = new MemoryStream();
            var capture = new CaptureStream(inner, 100);

            await capture.WriteAsync(Encoding.ASCII.GetBytes("hello "));
            capture.Write(Encoding.ASCII.GetBytes("world"), 0, 5);

            Assert.Equal("hello world", Encoding.ASCII.GetString(inner.ToArray()));
            Assert.Equal("hello world", Encoding.ASCII.GetString(capture.CapturedBytes));
            Assert.False(capture.Overflowed);
        }

        [Fact]
        public async Task Write_PastLimit_InnerGetsEverythingAndCaptureStops()
        {
            var inner = new MemoryStream();
            var capture = new CaptureStream(inner, 10);

            await capture.WriteAsync(Encoding.ASCII.GetBytes("12345678"));
            await capture.WriteAsync(Encoding.ASCII.GetBytes("abcdef"));
            await capture.WriteAsync(Encoding.ASCII.GetBytes("x"));

            Assert.Equal("12345678abcdefx", Encoding.ASCII.GetString(inner.ToArray()));
            Assert.True(capture.Overflowed);
            Assert.Equal("12345678", Encoding.ASCII.GetString(capture.CapturedBytes));
            Assert.True(capture.CapturedBytes.Length <= 10);
        }
    }
}
