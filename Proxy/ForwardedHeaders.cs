using System.Net;
using System.Text;

namespace Proxy
{
    public static class ForwardedHeaders
    {
        private static readonly string[] Replaced = ["X-Forwarded-For", "X-Forwarded-Proto"];

        public static byte[] Rewrite(string head, IPAddress clientIp, string proto)
        {
            string[] lines = head.Split("\r\n");
            StringBuilder builder = new StringBuilder();

            builder.Append(lines[0]).Append("\r\n");

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Length == 0)
                {
                    break;
                }

                int colon = line.IndexOf(':');
                string name = colon > 0 ? line.Substring(0, colon) : line;
                if (Replaced.Any(header => header.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                builder.Append(line).Append("\r\n");
            }

            IPAddress address = clientIp.IsIPv4MappedToIPv6 ? clientIp.MapToIPv4() : clientIp;

            builder.Append("X-Forwarded-For: ").Append(address).Append("\r\n");
            builder.Append("X-Forwarded-Proto: ").Append(proto).Append("\r\n");
            builder.Append("\r\n");

            return Encoding.ASCII.GetBytes(builder.ToString());
        }
    }
}
