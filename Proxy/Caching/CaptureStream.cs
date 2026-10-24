namespace Proxy.Caching
{
    public class CaptureStream : Stream
    {
        private readonly Stream _inner;
        private readonly int _maxCaptureBytes;
        private readonly MemoryStream _capture = new MemoryStream();

        public CaptureStream(Stream inner, int maxCaptureBytes)
        {
            _inner = inner;
            _maxCaptureBytes = maxCaptureBytes;
        }

        public bool Overflowed { get; private set; }

        public byte[] CapturedBytes => _capture.ToArray();

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.Write(buffer, offset, count);
            Capture(buffer.AsSpan(offset, count));
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _inner.WriteAsync(buffer, cancellationToken);
            Capture(buffer.Span);
        }

        public override void Flush() => _inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        private void Capture(ReadOnlySpan<byte> data)
        {
            if (Overflowed)
            {
                return;
            }

            if (_capture.Length + data.Length > _maxCaptureBytes)
            {
                Overflowed = true;
                return;
            }

            _capture.Write(data);
        }
    }
}
