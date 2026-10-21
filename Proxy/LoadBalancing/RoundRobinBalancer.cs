namespace Proxy.LoadBalancing
{
    public class RoundRobinBalancer : LoadBalancerBase
    {
        public RoundRobinBalancer(IReadOnlyList<Backend> backends) : base(backends)
        {
        }

        public override Backend? Pick()
        {
            int start = NextIndex();

            for (int i = 0; i < Backends.Length; i++)
            {
                int position = (start + i) % Backends.Length;
                if (Backends[position].IsHealthy)
                {
                    return Backends[position];
                }
            }

            return null;
        }
    }
}
