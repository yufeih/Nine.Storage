namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Xunit;

    public abstract class BlobStorageSpec<TData> : ITestFactoryData<IBlobStorage> where TData : ITestFactoryData<IBlobStorage>, new()
    {
        public static IEnumerable<object[]> Data = new TestFactoryDimension<TData, IBlobStorage>();

        public abstract IEnumerable<ITestFactory<IBlobStorage>> GetData();

        [Theory, MemberData("Data")]
        public async Task get_null_sha_returns_null(ITestFactory<IBlobStorage> storageFactory)
        {
            var storage = storageFactory.Create();
            Assert.Null(await storage.GetUri(null));
            Assert.Null(await storage.GetUri(""));
            Assert.Null(await storage.Get(null));
            Assert.Null(await storage.Get(""));
        }

        [Theory, MemberData("Data")]
        public async Task store_binaries_into_blob_storage(ITestFactory<IBlobStorage> storageFactory)
        {
            var storage = storageFactory.Create();

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
        public async Task should_not_dispose_input_stream(ITestFactory<IBlobStorage> storageFactory)
        {
            var storage = storageFactory.Create();
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
