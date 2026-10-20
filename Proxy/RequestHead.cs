namespace Proxy
{
    public class RequestHead
    {
        public byte[] Buffer { get; }
        public int HeadLength { get; }
        public int TotalRead { get; }
        public int LeftoverLength => TotalRead - HeadLength;

        public RequestHead(byte[] buffer, int headLength, int totalRead)
        {
            Buffer = buffer;
            HeadLength = headLength;
            TotalRead = totalRead;
        }
    }
}
