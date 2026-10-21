namespace Proxy.Tests
{
    public class ManualTimeProvider : TimeProvider
    {
        private long _now;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _now;

        public void Advance(TimeSpan by) => _now += by.Ticks;
    }
}
