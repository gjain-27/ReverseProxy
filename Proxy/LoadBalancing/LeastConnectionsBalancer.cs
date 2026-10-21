namespace Proxy.LoadBalancing
{
    public class LeastConnectionsBalancer : LoadBalancerBase
    {
        public LeastConnectionsBalancer(IReadOnlyList<Backend> backends) : base(backends)
        {
        }

        public override Backend Pick()
        {
            int start = NextIndex();
            Backend best = Backends[start];

            for (int i = 1; i < Backends.Length; i++)
            {
                Backend candidate = Backends[(start + i) % Backends.Length];

                if (candidate.ActiveConnections < best.ActiveConnections)
                {
                    best = candidate;
                }
            }


            return best;
        }
    }
}
