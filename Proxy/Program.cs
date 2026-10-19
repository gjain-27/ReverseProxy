namespace Proxy
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            TcpProxy myProxy = new TcpProxy(8080, "localhost", 5000);

            await myProxy.StartAsync();
        }
    }
}