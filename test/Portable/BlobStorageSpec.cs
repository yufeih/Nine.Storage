namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Xunit;

    public abstract class BlobStorageSpec<TData> : ITestData<Func<IBlobStorage>> where TData : ITestData<Func<IBlobStorage>>, new()
    {
        public static IEnumerable<object[]> Data = new TestDimension<TData, Func<IBlobStorage>>();

        public abstract IEnumerable<Func<IBlobStorage>> GetData();

        [Theory, MemberData("Data")]
        public async Task store_binaries_into_blob_storage(Func<IBlobStorage> storageFactory)
        {
            var storage = storageFactory();

            var bytes = Enumerable.Range(0, 1024).Select(x => (byte)x).ToArray();
            var stream = new MemoryStream(bytes);

            stream.Seek(0, SeekOrigin.Begin);
            var sha = await storage.Put(stream).ConfigureAwait(false);

            Assert.Equal("5b00669c480d5cffbdfa8bdba99561160f2d1b77", sha);
            Assert.True(await storage.Exists(sha));

            var read = await storage.Get(sha).ConfigureAwait(false);
            var stored = await read.ReadBytesAsync();

            Assert.True(bytes.SequenceEqual(stored));
        }

        [Theory, MemberData("Data")]
        public async Task should_not_dispose_input_stream(Func<IBlobStorage> storageFactory)
        {
            var storage = storageFactory();
            var random = new Random();

            var bytes = Enumerable.Range(0, 11).Select(x => (byte)random.Next(255)).ToArray();
            var stream = new MemoryStream(bytes);

            for (int i = 0; i < 2; i++)
            {
                stream.Seek(0, SeekOrigin.Begin);
                var sha = await storage.Put(stream).ConfigureAwait(false);
                var read = await storage.Get(sha).ConfigureAwait(false);
                var stored = await read.ReadBytesAsync();

                Assert.True(bytes.SequenceEqual(stored));
            }
        }
    }
}
