using System.Net;
using Proxy.RateLimiting;

namespace Proxy.Tests
{
    public class RateLimiterTests
    {
        private static readonly IPAddress ClientA = IPAddress.Parse("10.0.0.1");
        private static readonly IPAddress ClientB = IPAddress.Parse("10.0.0.2");

        [Fact]
        public void TryAcquire_DifferentClients_HaveSeparateBuckets()
        {
            var time = new ManualTimeProvider();
            var limiter = new RateLimiter(2, 0, time);

            Assert.True(limiter.TryAcquire(ClientA));
            Assert.True(limiter.TryAcquire(ClientA));
            Assert.False(limiter.TryAcquire(ClientA));

            Assert.True(limiter.TryAcquire(ClientB));
            Assert.True(limiter.TryAcquire(ClientB));
        }

        [Fact]
        public void TryAcquire_NewClient_CreatesBucket()
        {
            var time = new ManualTimeProvider();
            var limiter = new RateLimiter(10, 5, time);

            Assert.Equal(0, limiter.TrackedClients);

            limiter.TryAcquire(ClientA);

            Assert.Equal(1, limiter.TrackedClients);
        }

        [Fact]
        public void RemoveIdle_IdleBucket_IsRemoved()
        {
            var time = new ManualTimeProvider();
            var limiter = new RateLimiter(10, 5, time);
            limiter.TryAcquire(ClientA);

            time.Advance(TimeSpan.FromMinutes(10));
            limiter.RemoveIdle(TimeSpan.FromMinutes(5));

            Assert.Equal(0, limiter.TrackedClients);
        }

        [Fact]
        public void RemoveIdle_RecentlyUsedBucket_IsKept()
        {
            var time = new ManualTimeProvider();
            var limiter = new RateLimiter(10, 5, time);
            limiter.TryAcquire(ClientA);

            time.Advance(TimeSpan.FromMinutes(1));
            limiter.RemoveIdle(TimeSpan.FromMinutes(5));

            Assert.Equal(1, limiter.TrackedClients);
        }
    }
}
