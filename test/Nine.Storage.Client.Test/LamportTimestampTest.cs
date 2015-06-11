namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public class LamportTimestampTest
    {
        [Fact]
        public static void it_should_generate_unique_timestamps()
        {
            var count = 1000;
            var now = DateTime.UtcNow;
            var cap = DateTime.UtcNow.AddHours(1);
            var stamp = new LamportTimestamp();
            var stamps = Enumerable.Range(0, count).AsParallel().Select(i => stamp.Next()).ToList();
            Assert.Equal(count, stamps.Distinct().Count());
            Assert.True(stamps.All(s => s >= now && s < cap && s.Kind == DateTimeKind.Utc));
        }

        [Fact]
        public static void it_should_generate_sequential_timestamps()
        {
            var stamp = new LamportTimestamp();
            var stamps = Enumerable.Range(0, 1000).Select(i => stamp.Next()).ToList();
            var sorted = new List<DateTime>(stamps);
            sorted.Sort();
            Assert.Equal(stamps, sorted);
        }
    }
}
