using System.Net.Sockets;

namespace Proxy
{
    public class RequestReader
    {
        private const int MaxHeadSize = 8192;
        private static readonly TimeSpan HeadTimeout = TimeSpan.FromSeconds(10);

        private readonly NetworkStream _stream;
        private readonly byte[] _buffer = new byte[MaxHeadSize];
        private int _start;
        private int _end;

        public RequestReader(NetworkStream stream)
        {
            _stream = stream;
        }

        public async Task<ReadOnlyMemory<byte>?> ReadHeadAsync()
        {
            using CancellationTokenSource timeout = new CancellationTokenSource(HeadTimeout);

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
                throw new TimeoutException($"Timed out after {HeadTimeout.TotalSeconds}s waiting for request head");
            }
        }
    }
}
