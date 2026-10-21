namespace Proxy.LoadBalancing
{
    public abstract class LoadBalancerBase : ILoadBalancer
    {
        protected readonly Backend[] Backends;
        private long _counter = -1;

        protected LoadBalancerBase(IReadOnlyList<Backend> backends)
        {
            if (backends.Count == 0)
            {
                throw new ArgumentException("At least one backend is required", nameof(backends));
            }

            Backends = backends.ToArray();
        }

        protected int NextIndex()
        {
            long next = Interlocked.Increment(ref _counter);
            return (int)(next % Backends.Length);
        }

        public abstract Backend Pick();
    }
}
