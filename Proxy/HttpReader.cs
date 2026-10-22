using System.Globalization;
using System.Text;

namespace Proxy
{
    public class HttpReader
    {
        private const int MaxHeadSize = 8192;
        private const int MaxLineSize = 1024;

        private readonly Stream _stream;
        private readonly TimeSpan _headTimeout;
        private readonly byte[] _buffer = new byte[MaxHeadSize];
        private int _start;
        private int _end;

        public HttpReader(Stream stream) : this(stream, TimeSpan.FromSeconds(10))
        {
        }

        public HttpReader(Stream stream, TimeSpan headTimeout)
        {
            _stream = stream;
            _headTimeout = headTimeout;
        }

        public async Task<ReadOnlyMemory<byte>?> ReadHeadAsync()
        {
            using CancellationTokenSource timeout = new CancellationTokenSource(_headTimeout);

            try
            {
                while (true)
                {
                    int index = _buffer.AsSpan(_start, _end - _start).IndexOf("\r\n\r\n"u8);

                    if (index >= 0)
                    {
                        int headLength = index + 4;
                        ReadOnlyMemory<byte> head = _buffer.AsMemory(_start, headLength);
                        _start += headLength;
                        return head;
                    }

                    if (_start > 0)
                    {
                        _buffer.AsSpan(_start, _end - _start).CopyTo(_buffer);
                        _end -= _start;
                        _start = 0;
                    }

                    if (_end >= MaxHeadSize)
                    {
                        throw new InvalidDataException($"Request head exceeds {MaxHeadSize} bytes");
                    }

                    int bytesRead = await _stream.ReadAsync(_buffer.AsMemory(_end), timeout.Token);

                    if (bytesRead == 0)
                    {
                        if (_start == _end)
                        {
                            return null;
                        }

                        throw new IOException($"Connection closed with {_end - _start} bytes of an incomplete request head");
                    }

                    _end += bytesRead;
                }
            } catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                throw new TimeoutException($"Timed out after {_headTimeout.TotalSeconds}s waiting for request head");
            }
        }

        public async Task CopyBodyAsync(long length, Stream destination)
        {
            long remaining = length;

            int fromBuffer = (int)Math.Min(remaining, _end - _start);

            if (fromBuffer > 0)
            {
                await destination.WriteAsync(_buffer.AsMemory(_start, fromBuffer));
                _start += fromBuffer;
                remaining -= fromBuffer;
            }

            while (remaining > 0)
            {
                _start = 0;
                _end = 0;

                int toRead = (int)Math.Min(remaining, _buffer.Length);
                int bytesRead = await _stream.ReadAsync(_buffer.AsMemory(0, toRead));

                if (bytesRead == 0)
                {
                    throw new IOException($"Connection closed with {remaining} of {length} body bytes not received");
                }

                await destination.WriteAsync(_buffer.AsMemory(0, bytesRead));
                remaining -= bytesRead;
            }
        }

        public async Task<ReadOnlyMemory<byte>> ReadLineAsync()
        {
            using CancellationTokenSource timeout = new CancellationTokenSource(_headTimeout);

            try
            {
                while (true)
                {
                    int window = Math.Min(_end - _start, MaxLineSize);
                    int index = _buffer.AsSpan(_start, window).IndexOf("\r\n"u8);

                    if (index >= 0)
                    {
                        int lineLength = index + 2;
                        ReadOnlyMemory<byte> line = _buffer.AsMemory(_start, lineLength);
                        _start += lineLength;
                        return line;
                    }

                    if (_end - _start >= MaxLineSize)
                    {
                        throw new InvalidDataException($"Line exceeds {MaxLineSize} bytes");
                    }

                    if (_start > 0)
                    {
                        _buffer.AsSpan(_start, _end - _start).CopyTo(_buffer);
                        _end -= _start;
                        _start = 0;
                    }

                    int bytesRead = await _stream.ReadAsync(_buffer.AsMemory(_end), timeout.Token);

                    if (bytesRead == 0)
                    {
                        throw new IOException($"Connection closed with {_end - _start} bytes of an incomplete line");
                    }

                    _end += bytesRead;
                }
            } catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                throw new TimeoutException($"Timed out after {_headTimeout.TotalSeconds}s waiting for a line");
            }
        }

        public async Task CopyChunkedBodyAsync(Stream destination)
        {
            while (true)
            {
                ReadOnlyMemory<byte> sizeLine = await ReadLineAsync();
                long size = ParseChunkSize(sizeLine);
                await destination.WriteAsync(sizeLine);

                if (size == 0)
                {
                    while (true)
                    {
                        ReadOnlyMemory<byte> trailer = await ReadLineAsync();
                        await destination.WriteAsync(trailer);

                        if (trailer.Span.SequenceEqual("\r\n"u8))
                        {
                            return;
                        }
                    }
                }

                await CopyBodyAsync(size, destination);

                ReadOnlyMemory<byte> crlf = await ReadLineAsync();
                if (!crlf.Span.SequenceEqual("\r\n"u8))
                {
                    throw new FormatException("Missing CRLF after chunk data");
                }

                await destination.WriteAsync(crlf);
            }
        }

        public async Task CopyUntilCloseAsync(Stream destination)
        {
            if (_end > _start)
            {
                await destination.WriteAsync(_buffer.AsMemory(_start, _end - _start));
            }

            _start = 0;
            _end = 0;

            await _stream.CopyToAsync(destination);
        }

        private static long ParseChunkSize(ReadOnlyMemory<byte> line)
        {
            string text = Encoding.ASCII.GetString(line.Span[..^2]);

            int semicolon = text.IndexOf(';');
            if (semicolon >= 0)
            {
                text = text.Substring(0, semicolon);
            }

            text = text.Trim();

            if (text.Length == 0 || !text.All(Uri.IsHexDigit) || !long.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out long size) || size < 0)
            {
                throw new FormatException($"Invalid chunk size: '{text}'");
            }

            return size;
        }
    }
}
