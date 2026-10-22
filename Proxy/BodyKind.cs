namespace Proxy
{
    public enum BodyKind
    {
        None,
        ContentLength,
        Chunked,
        UntilClose
    }
}
