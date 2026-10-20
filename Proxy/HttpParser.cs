using System.Globalization;

namespace Proxy
{
    public static class HttpParser
    {
        public static HttpRequest Parse(string head)
        {
            HttpRequest request = new HttpRequest();

            string[] lines = head.Split("\r\n");

            RejectBareLineBreaks(lines[0]);

            string[] requestLine = lines[0].Split(' ');
            if (requestLine.Length != 3 || requestLine.Any(string.IsNullOrEmpty))
            {
                throw new FormatException($"Malformed request line: '{lines[0]}'");
            }
            if (!requestLine[2].StartsWith("HTTP/", StringComparison.Ordinal))
            {
                throw new FormatException($"Invalid HTTP version: '{requestLine[2]}'");
            }

            request.Method = requestLine[0];
            request.Path = requestLine[1];
            request.Version = requestLine[2];

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Length == 0)
                {
                    break;
                }

                RejectBareLineBreaks(line);

                int colon = line.IndexOf(':');
                if (colon <= 0)
                {
                    throw new FormatException($"Malformed header line: '{line}'");
                }

                string name = line.Substring(0, colon);
                if (name.Any(char.IsWhiteSpace))
                {
                    throw new FormatException($"Whitespace in header name: '{name}'");
                }

                string value = line.Substring(colon + 1).Trim();

                if (request.Headers.TryGetValue(name, out string? existing))
                {
                    if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                    {
                        if (existing != value)
                        {
                            throw new FormatException($"Conflicting Content-Length values: '{existing}' and '{value}'");
                        }
                    }
                    else
                    {
                        request.Headers[name] = existing + ", " + value;
                    }
                }
                else
                {
                    request.Headers[name] = value;
                }
            }

            return request;
        }

        public static long GetBodyLength(HttpRequest request)
        {
            bool hasContentLength = request.Headers.TryGetValue("Content-Length", out string? contentLength);
            bool hasTransferEncoding = request.Headers.ContainsKey("Transfer-Encoding");

            if (hasContentLength && hasTransferEncoding)
            {
                throw new FormatException("Request has both Content-Length and Transfer-Encoding");
            }

            if (hasTransferEncoding)
            {
                throw new NotSupportedException("Transfer-Encoding is not supported");
            }

            if (hasContentLength)
            {
                if (!long.TryParse(contentLength, NumberStyles.None, CultureInfo.InvariantCulture, out long length))
                {
                    throw new FormatException($"Invalid Content-Length: '{contentLength}'");
                }

                return length;
            }

            return 0;
        }

        private static void RejectBareLineBreaks(string line)
        {
            if (line.Contains('\r') || line.Contains('\n'))
            {
                throw new FormatException($"Bare CR or LF in line: '{line}'");
            }
        }
    }
}
