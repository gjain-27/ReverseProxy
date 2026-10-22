using System.Text;
using System.Threading.Channels;

namespace Proxy
{
    public static class ResponseForwarder
    {
        public static async Task ForwardAsync(HttpReader backendReader, ChannelReader<ExpectedResponse> expected, Stream client)
        {
            await foreach (ExpectedResponse item in expected.ReadAllAsync())
            {
                while (true)
                {
                    ReadOnlyMemory<byte>? head = await backendReader.ReadHeadAsync();
                    if (head == null)
                    {
                        throw new IOException("Backend closed the connection before responding");
                    }

                    HttpResponse response = HttpParser.ParseResponse(Encoding.ASCII.GetString(head.Value.Span));

                    await client.WriteAsync(head.Value);

                    Console.WriteLine($"Response: {response.StatusCode} {response.Reason}");

                    if (response.StatusCode == 101)
                    {
                        return;
                    }

                    if (response.StatusCode >= 100 && response.StatusCode <= 199)
                    {
                        continue;
                    }

                    var (kind, length) = HttpParser.GetResponseBody(item.Method, response);

                    switch (kind)
                    {
                        case BodyKind.ContentLength:
                            await backendReader.CopyBodyAsync(length, client);
                            break;
                        case BodyKind.Chunked:
                            await backendReader.CopyChunkedBodyAsync(client);
                            break;
                        case BodyKind.UntilClose:
                            await backendReader.CopyUntilCloseAsync(client);
                            return;
                    }

                    break;
                }
            }
        }
    }
}
