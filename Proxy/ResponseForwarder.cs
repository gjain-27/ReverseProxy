using Proxy.Caching;
using System.Text;
using System.Threading.Channels;

namespace Proxy
{
    public static class ResponseForwarder
    {
        public static async Task ForwardAsync(HttpReader backendReader, ChannelReader<ExpectedResponse> expected, Stream client, LruCache cache, TimeProvider time)
        {
            await foreach (ExpectedResponse item in expected.ReadAllAsync())
            {
                if (item.Cached != null)
                {
                    CachedResponse cached = item.Cached;
                    long age = (long)time.GetElapsedTime(cached.StoredAt).TotalSeconds;
                    await client.WriteAsync(AddHeaders(cached.HeadText, "X-Cache: HIT", $"Age: {age}"));
                    await client.WriteAsync(cached.Body);
                    continue;
                }

                while (true)
                {
                    ReadOnlyMemory<byte>? head = await backendReader.ReadHeadAsync();
                    if (head == null)
                    {
                        throw new IOException("Backend closed the connection before responding");
                    }

                    string headText = Encoding.ASCII.GetString(head.Value.Span);
                    HttpResponse response = HttpParser.ParseResponse(headText);

                    await client.WriteAsync(AddHeaders(headText, "X-Cache: MISS"));

                    Log.Request($"Response: {response.StatusCode} {response.Reason}");

                    if (response.StatusCode == 101)
                    {
                        return;
                    }

                    if (response.StatusCode >= 100 && response.StatusCode <= 199)
                    {
                        continue;
                    }

                    var (kind, length) = HttpParser.GetResponseBody(item.Method, response);

                    TimeSpan? ttl = null;
                    if (item.CacheKey != null && kind != BodyKind.UntilClose)
                    {
                        ttl = CachePolicy.GetTimeToLive(response);
                    }

                    if (ttl != null)
                    {
                        CaptureStream capture = new CaptureStream(client, CachePolicy.MaxEntryBytes);
                        await CopyBodyAsync(backendReader, kind, length, capture);

                        if (!capture.Overflowed)
                        {
                            cache.Set(item.CacheKey!, new CachedResponse(headText, capture.CapturedBytes, time.GetTimestamp(), ttl.Value));
                            Log.Request($"Cached {item.CacheKey} for {ttl.Value.TotalSeconds}s");
                        }
                    }
                    else
                    {
                        await CopyBodyAsync(backendReader, kind, length, client);

                        if (kind == BodyKind.UntilClose)
                        {
                            return;
                        }
                    }

                    break;
                }
            }
        }

        private static async Task CopyBodyAsync(HttpReader backendReader, BodyKind kind, long length, Stream destination)
        {
            switch (kind)
            {
                case BodyKind.ContentLength:
                    await backendReader.CopyBodyAsync(length, destination);
                    break;
                case BodyKind.Chunked:
                    await backendReader.CopyChunkedBodyAsync(destination);
                    break;
                case BodyKind.UntilClose:
                    await backendReader.CopyUntilCloseAsync(destination);
                    break;
            }
        }

        private static byte[] AddHeaders(string headText, params string[] headerLines)
        {
            StringBuilder builder = new StringBuilder(headText, 0, headText.Length - 2, headText.Length + 64);

            foreach (string line in headerLines)
            {
                builder.Append(line).Append("\r\n");
            }

            builder.Append("\r\n");

            return Encoding.ASCII.GetBytes(builder.ToString());
        }
    }
}
