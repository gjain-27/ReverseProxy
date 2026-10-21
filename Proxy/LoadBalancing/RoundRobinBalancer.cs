namespace Proxy.LoadBalancing
{
    public class RoundRobinBalancer : LoadBalancerBase
    {
        public RoundRobinBalancer(IReadOnlyList<Backend> backends) : base(backends)
        {
        }

        public override Backend Pick() => Backends[NextIndex()];
    }
}
