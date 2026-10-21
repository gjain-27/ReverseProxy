namespace Proxy.LoadBalancing
{
    public interface ILoadBalancer
    {
        Backend Pick();
    }
}
