namespace Proxy
{
    public class HttpResponse
    {
        public string Version { get; set; } = "";
        public int StatusCode { get; set; }
        public string Reason { get; set; } = "";
        public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
