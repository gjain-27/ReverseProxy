using System.Collections.Concurrent;
using System.Net;

namespace Proxy.RateLimiting
{
    public class RateLimiter
    {
        private readonly int _capacity;
        private readonly double _refillPerSecond;
        private readonly TimeProvider _time;
        private readonly ConcurrentDictionary<IPAddress, TokenBucket> _buckets = new();

        public RateLimiter(int capacity, double refillPerSecond, TimeProvider time)
        {
            _capacity = capacity;
            _refillPerSecond = refillPerSecond;
            _time = time;
        }

        public int TrackedClients => _buckets.Count;

        public bool TryAcquire(IPAddress client)
        {
            TokenBucket bucket = _buckets.GetOrAdd(client, _ => new TokenBucket(_capacity, _refillPerSecond, _time));

            return bucket.TryTake();
        }

        public void RemoveIdle(TimeSpan idleTime)
        {
            foreach (var pair in _buckets)
            {
                if (pair.Value.IsIdle(idleTime))
                {
                    _buckets.TryRemove(pair.Key, out _);
                }
            }
        }

        public async Task RunCleanupAsync(TimeSpan interval, TimeSpan idleTime)
        {
            while (true)
            {
                try
                {
                    RemoveIdle(idleTime);
                } catch (Exception e)
                {
                    Log.Info($"Rate limiter cleanup error: {e.Message}");
                }

                await Task.Delay(interval);
            }
        }
    }
}
