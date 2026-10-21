namespace Proxy.LoadBalancing
{
    public class LeastConnectionsBalancer : LoadBalancerBase
    {
        public LeastConnectionsBalancer(IReadOnlyList<Backend> backends) : base(backends)
        {
        }

        public override Backend? Pick()
        {
            int start = NextIndex();
            Backend? best = null;

            for (int i = 0; i < Backends.Length; i++)
            {
                Backend candidate = Backends[(start + i) % Backends.Length];

                if (!candidate.IsHealthy) 
                {
                    continue;
                }

                if (best == null || candidate.ActiveConnections < best.ActiveConnections)
                {
                    best = candidate;
                }
            }


            return best;
        }
    }
}
