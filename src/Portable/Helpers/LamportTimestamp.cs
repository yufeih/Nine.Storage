namespace System
{
    using System.Threading;

    /// <summary>
    /// Represents the lamport timestamp of this process.
    /// </summary>
    /// <remarks>
    ///  http://en.wikipedia.org/wiki/Lamport_timestamps
    /// </remarks>
    class LamportTimestamp
    {
        private long stamp;

        public LamportTimestamp()
        {
            stamp = DateTime.UtcNow.Ticks;
        }

        public LamportTimestamp(DateTime initial)
        {
            stamp = initial.Ticks;
        }

        public DateTime Next()
        {
            var now = DateTime.UtcNow.Ticks;
            var last = stamp;

            if (now <= last)
            {
                return new DateTime(Interlocked.Increment(ref stamp), DateTimeKind.Utc);
            }

            if (Interlocked.CompareExchange(ref stamp, now, last) == last)
            {
                return new DateTime(now, DateTimeKind.Utc);
            }

            return new DateTime(Interlocked.Increment(ref stamp), DateTimeKind.Utc);
        }
    }
}