namespace Proxy.LoadBalancing
{
    public class RoundRobinBalancer : ILoadBalancer
    {
        private readonly Backend[] _backends;
        private long _counter = -1;

        public RoundRobinBalancer(IReadOnlyList<Backend> backends)
        {
            if (backends.Count == 0)
            {
                throw new ArgumentException("At least one backend is required", nameof(backends));
            }

            _backends = backends.ToArray();
        }

        public Backend Pick()
        {
            long next = Interlocked.Increment(ref _counter);
            return _backends[next % _backends.Length];
        }
    }
}
