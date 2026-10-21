namespace Proxy.RateLimiting
{
    public class TokenBucket
    {
        private double _tokens;
        private long _lastRefill;
        private readonly object _sync = new();
        private readonly int _capacity;
        private readonly double _refillPerSecond;
        private readonly TimeProvider _time;

        public TokenBucket(int capacity, double refillPerSecond, TimeProvider time)
        {
            _capacity = capacity;
            _refillPerSecond = refillPerSecond;
            _time = time;

            _tokens = capacity;
            _lastRefill = time.GetTimestamp();
        }

        public bool TryTake()
        {
            lock (_sync)
            {
                double elapsed = _time.GetElapsedTime(_lastRefill).TotalSeconds;
                _tokens = Math.Min(_capacity, _tokens + elapsed * _refillPerSecond);
                _lastRefill = _time.GetTimestamp();
                if (_tokens >= 1)
                {
                    _tokens -= 1;
                    return true;
                }
            }

            return false;
        }

        public bool IsIdle(TimeSpan idleTime)
        {
            lock (_sync)
            {
                return _time.GetElapsedTime(_lastRefill) >= idleTime;
            }
        }
    }
}
