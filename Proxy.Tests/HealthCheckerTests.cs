using System.Net;
using System.Net.Sockets;
using Proxy.LoadBalancing;

namespace Proxy.Tests
{
    public class HealthCheckerTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

        private static TcpListener StartListener(int port = 0)
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return listener;
        }

        private static int PortOf(TcpListener listener)
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        private static int GetFreePort()
        {
            TcpListener listener = StartListener();
            int port = PortOf(listener);
            listener.Stop();
            return port;
        }

        private static HealthChecker CreateChecker(params Backend[] backends)
        {
            return new HealthChecker(backends, TimeSpan.FromSeconds(5), Timeout);
        }

        [Fact]
        public async Task CheckAllAsync_BackendListening_MarksHealthy()
        {
            TcpListener listener = StartListener();
            try
            {
                Backend backend = new Backend("127.0.0.1", PortOf(listener));
                backend.MarkUnhealthy();

                await CreateChecker(backend).CheckAllAsync();

                Assert.True(backend.IsHealthy);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Fact]
        public async Task CheckAllAsync_NothingListening_MarksUnhealthy()
        {
            Backend backend = new Backend("127.0.0.1", GetFreePort());

            await CreateChecker(backend).CheckAllAsync();

            Assert.False(backend.IsHealthy);
        }

        [Fact]
        public async Task CheckAllAsync_BackendComesBack_MarksHealthyAgain()
        {
            int port = GetFreePort();
            Backend backend = new Backend("127.0.0.1", port);
            HealthChecker checker = CreateChecker(backend);

            await checker.CheckAllAsync();
            Assert.False(backend.IsHealthy);

            TcpListener listener = StartListener(port);
            try
            {
                await checker.CheckAllAsync();
                Assert.True(backend.IsHealthy);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Fact]
        public async Task CheckAllAsync_MultipleBackends_EachMarkedCorrectly()
        {
            TcpListener listener = StartListener();
            try
            {
                Backend up = new Backend("127.0.0.1", PortOf(listener));
                Backend down = new Backend("127.0.0.1", GetFreePort());

                await CreateChecker(up, down).CheckAllAsync();

                Assert.True(up.IsHealthy);
                Assert.False(down.IsHealthy);
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
