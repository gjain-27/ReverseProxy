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

            ParseHeaders(lines, request.Headers);

            return request;
        }

        public static HttpResponse ParseResponse(string head)
        {
            HttpResponse response = new HttpResponse();

            string[] lines = head.Split("\r\n");

            RejectBareLineBreaks(lines[0]);

            string[] statusLine = lines[0].Split(' ', 3);
            if (statusLine.Length < 2 || statusLine.Length > 3)
            {
                throw new FormatException($"Malformed status line: '{lines[0]}'");
            }
            if (!statusLine[0].StartsWith("HTTP/", StringComparison.Ordinal))
            {
                throw new FormatException($"Invalid HTTP version: '{statusLine[0]}'");
            }

            string code = statusLine[1];
            if (code.Length != 3 || !int.TryParse(code, NumberStyles.None, CultureInfo.InvariantCulture, out int statusCode) || statusCode < 100 || statusCode > 599)
            {
                throw new FormatException($"Invalid status code: '{code}'");
            }

            response.Version = statusLine[0];
            response.StatusCode = statusCode;
            response.Reason = statusLine.Length == 3 ? statusLine[2] : "";

            ParseHeaders(lines, response.Headers);

            return response;
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
                return ParseContentLength(contentLength!);
            }

            return 0;
        }

        public static (BodyKind Kind, long Length) GetResponseBody(string requestMethod, HttpResponse response)
        {
            if (requestMethod.Equals("HEAD", StringComparison.Ordinal))
            {
                return (BodyKind.None, 0);
            }

            int status = response.StatusCode;
            if ((status >= 100 && status <= 199) || status == 204 || status == 304)
            {
                return (BodyKind.None, 0);
            }

            if (response.Headers.TryGetValue("Transfer-Encoding", out string? transferEncoding)
                && transferEncoding.Contains("chunked", StringComparison.OrdinalIgnoreCase))
            {
                return (BodyKind.Chunked, 0);
            }

            if (response.Headers.TryGetValue("Content-Length", out string? contentLength))
            {
                return (BodyKind.ContentLength, ParseContentLength(contentLength));
            }

            return (BodyKind.UntilClose, 0);
        }

        private static long ParseContentLength(string value)
        {
            if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long length))
            {
                throw new FormatException($"Invalid Content-Length: '{value}'");
            }

            return length;
        }

        private static void ParseHeaders(string[] lines, Dictionary<string, string> headers)
        {
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

                if (headers.TryGetValue(name, out string? existing))
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
                        headers[name] = existing + ", " + value;
                    }
                }
                else
                {
                    headers[name] = value;
                }
            }
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
