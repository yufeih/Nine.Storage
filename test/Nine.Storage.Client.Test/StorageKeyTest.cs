namespace Nine.Storage
{
    using System;
    using System.Linq;
    using Xunit;
    
    public class StorageKeyTest
    {
        [Fact]
        public static void should_only_accept_numbers_and_letters()
        {
            foreach (var c in new[] { "-", "/", "\\", "'", "\"", "@", "!", "$", "*" })
            {
                Assert.Throws<ArgumentException>(() => StorageKey.Get(c, "1"));
            }
        }

        [Fact]
        public static void should_accept_everything_for_the_last_component()
        {
            foreach (var c in new[] { "-", "/", "\\", "'", "\"", "@", "!", "$", "*" })
            {
                StorageKey.Get("1", c);
            }
        }

        [Fact]
        public static void should_sort_values_based_on_lexicographical_order()
        {
            GetShouldEqual(new[] { ulong.MinValue, (ulong)48786, ulong.MaxValue });
            GetShouldEqual(new[] { long.MinValue, -20, 0, 48786, long.MaxValue });
            GetShouldEqual(new[] { uint.MinValue, (uint)48786, uint.MaxValue });
            GetShouldEqual(new[] { int.MinValue, -20, 0, 48786, int.MaxValue });
            GetShouldEqual(new[] { ushort.MinValue, (ushort)48786, ushort.MaxValue });
            GetShouldEqual(new[] { short.MinValue, -20, 0, 48, short.MaxValue });
            GetShouldEqual(new[] { byte.MinValue, (byte)123, byte.MaxValue });
            GetShouldEqual(new[] { sbyte.MinValue, -20, 0, 48, sbyte.MaxValue });
            GetShouldEqual(new[] { DateTime.MinValue, DateTime.UtcNow, DateTime.MaxValue });
            GetShouldEqual(new[] { TimeSpan.MinValue, TimeSpan.FromTicks(-1), TimeSpan.Zero, TimeSpan.FromTicks(1), TimeSpan.MaxValue });
        }

        private static void GetShouldEqual<T>(T[] data)
        {
            Assert.True(data.SequenceEqual(from x in data let key = StorageKey.Get(x) orderby key select x));
        }

        [Fact]
        public static void parse_should_be_correct()
        {
            ParseShouldEqual(new[] { true, false });
            ParseShouldEqual(new[] { ulong.MinValue, (ulong)48786, ulong.MaxValue });
            ParseShouldEqual(new[] { long.MinValue, -20, 0, 48786, long.MaxValue });
            ParseShouldEqual(new[] { uint.MinValue, (uint)48786, uint.MaxValue });
            ParseShouldEqual(new[] { int.MinValue, -20, 0, 48786, int.MaxValue });
            ParseShouldEqual(new[] { ushort.MinValue, (ushort)48786, ushort.MaxValue });
            ParseShouldEqual(new[] { short.MinValue, -20, 0, 48, short.MaxValue });
            ParseShouldEqual(new[] { byte.MinValue, (byte)123, byte.MaxValue });
            ParseShouldEqual(new[] { sbyte.MinValue, -20, 0, 48, sbyte.MaxValue });
            ParseShouldEqual(new[] { DateTime.MinValue, DateTime.UtcNow, DateTime.MaxValue });
            ParseShouldEqual(new[] { TimeSpan.MinValue, TimeSpan.FromTicks(-1), TimeSpan.Zero, TimeSpan.FromTicks(1), TimeSpan.MaxValue });
        }

        private static void ParseShouldEqual<T>(T[] data)
        {
            Assert.True(data.SequenceEqual(from x in data select StorageKey.Parse<T>(StorageKey.Get(x))));
        }

        [Fact]
        public static void increment_a_storage_key()
        {
            foreach (var key in new[] { null, "", "a", "1", "asfdsdfss", "a-", "-" })
            {
                Assert.True(StorageKey.IsIncrement(key, StorageKey.Increment(key)));
            }

            Assert.False(StorageKey.IsIncrement("aa", ""));
            Assert.False(StorageKey.IsIncrement("a", "a"));
            Assert.False(StorageKey.IsIncrement("a", null));
        }
    }
}
