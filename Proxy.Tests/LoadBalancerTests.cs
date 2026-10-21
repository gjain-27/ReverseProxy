using Proxy.LoadBalancing;

namespace Proxy.Tests
{
    public class LoadBalancerTests
    {
        private static Backend[] CreateBackends(int count)
        {
            Backend[] backends = new Backend[count];
            for (int i = 0; i < count; i++)
            {
                backends[i] = new Backend("localhost", 5000 + i);
            }

            return backends;
        }

        private static Backend[] CreateBackendsWithConnections(params int[] counts)
        {
            Backend[] backends = CreateBackends(counts.Length);
            for (int i = 0; i < counts.Length; i++)
            {
                for (int j = 0; j < counts[i]; j++)
                {
                    backends[i].OnConnectionOpened();
                }
            }

            return backends;
        }

        [Fact]
        public void RoundRobin_Pick_RotatesInOrder()
        {
            Backend[] backends = CreateBackends(3);
            var balancer = new RoundRobinBalancer(backends);

            for (int i = 0; i < 6; i++)
            {
                Assert.Same(backends[i % 3], balancer.Pick());
            }
        }

        [Fact]
        public void RoundRobin_Constructor_EmptyList_Throws()
        {
            Assert.Throws<ArgumentException>(() => new RoundRobinBalancer(new List<Backend>()));
        }

        [Fact]
        public void RoundRobin_Pick_ConcurrentCalls_DistributesExactly()
        {
            Backend[] backends = CreateBackends(3);
            var balancer = new RoundRobinBalancer(backends);
            int[] counts = new int[3];

            Parallel.For(0, 30000, _ => {
                int index = Array.IndexOf(backends, balancer.Pick());
                Interlocked.Increment(ref counts[index]);
            });

            Assert.All(counts, count => Assert.Equal(10000, count));
        }

        [Fact]
        public void LeastConnections_Pick_ReturnsLeastBusy()
        {
            Backend[] backends = CreateBackendsWithConnections(5, 1, 3);
            var balancer = new LeastConnectionsBalancer(backends);

            for (int i = 0; i < 6; i++)
            {
                Assert.Same(backends[1], balancer.Pick());
            }
        }

        [Fact]
        public void LeastConnections_Pick_LeastBusyInLastPosition_IsAlwaysFound()
        {
            Backend[] backends = CreateBackendsWithConnections(2, 2, 0);
            var balancer = new LeastConnectionsBalancer(backends);

            for (int i = 0; i < 6; i++)
            {
                Assert.Same(backends[2], balancer.Pick());
            }
        }

        [Fact]
        public void LeastConnections_Pick_AllTied_Rotates()
        {
            Backend[] backends = CreateBackends(3);
            var balancer = new LeastConnectionsBalancer(backends);

            for (int i = 0; i < 6; i++)
            {
                Assert.Same(backends[i % 3], balancer.Pick());
            }
        }

        [Fact]
        public void LeastConnections_Constructor_EmptyList_Throws()
        {
            Assert.Throws<ArgumentException>(() => new LeastConnectionsBalancer(new List<Backend>()));
        }
    }
}
