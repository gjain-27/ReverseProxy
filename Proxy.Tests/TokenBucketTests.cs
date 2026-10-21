using Proxy.RateLimiting;

namespace Proxy.Tests
{
    public class TokenBucketTests
    {
        private static int TakeAll(TokenBucket bucket)
        {
            int taken = 0;
            while (bucket.TryTake())
            {
                taken++;
            }

            return taken;
        }

        [Fact]
        public void TryTake_NewBucket_AllowsFullBurst()
        {
            var time = new ManualTimeProvider();
            var bucket = new TokenBucket(10, 5, time);

            for (int i = 0; i < 10; i++)
            {
                Assert.True(bucket.TryTake());
            }
        }

        [Fact]
        public void TryTake_Empty_ReturnsFalse()
        {
            var time = new ManualTimeProvider();
            var bucket = new TokenBucket(10, 5, time);

            for (int i = 0; i < 10; i++)
            {
                Assert.True(bucket.TryTake());
            }

            Assert.False(bucket.TryTake());
        }

        [Fact]
        public void TryTake_AfterOneSecond_RefillsAtRate()
        {
            var time = new ManualTimeProvider();
            var bucket = new TokenBucket(10, 5, time);
            TakeAll(bucket);

            time.Advance(TimeSpan.FromSeconds(1));

            for (int i = 0; i < 5; i++)
            {
                Assert.True(bucket.TryTake());
            }
            Assert.False(bucket.TryTake());
        }

        [Fact]
        public void TryTake_PartialTokens_Accumulate()
        {
            var time = new ManualTimeProvider();
            var bucket = new TokenBucket(10, 5, time);
            TakeAll(bucket);

            time.Advance(TimeSpan.FromMilliseconds(100));
            Assert.False(bucket.TryTake());

            time.Advance(TimeSpan.FromMilliseconds(100));
            Assert.True(bucket.TryTake());
        }

        [Fact]
        public void TryTake_LongIdle_NeverExceedsCapacity()
        {
            var time = new ManualTimeProvider();
            var bucket = new TokenBucket(10, 5, time);
            TakeAll(bucket);

            time.Advance(TimeSpan.FromHours(1));

            for (int i = 0; i < 10; i++)
            {
                Assert.True(bucket.TryTake());
            }
            Assert.False(bucket.TryTake());
        }

        [Fact]
        public void TryTake_ConcurrentCalls_NeverExceedsCapacity()
        {
            var time = new ManualTimeProvider();
            var bucket = new TokenBucket(1000, 0, time);
            int successes = 0;

            Parallel.For(0, 5000, _ =>
            {
                if (bucket.TryTake())
                {
                    Interlocked.Increment(ref successes);
                }
            });

            Assert.Equal(1000, successes);
        }
    }
}
