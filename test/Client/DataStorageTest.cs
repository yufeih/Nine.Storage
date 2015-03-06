namespace Nine.Storage
{
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;
    
    public class DataStorageTest
    {
        class TestStorageProvider : StorageProviderBase
        {
            public static int InitializeCount = 0;

            protected async override Task<IStorage<T>> CreateAsync<T>()
            {
                Interlocked.Increment(ref InitializeCount);
                await Task.Delay(200);
                return new MemoryStorage<T>();
            }
        }

        [Fact]
        public static async Task storage_provider_should_only_be_initialized_once()
        {
            var storage = new Storage(new TestStorageProvider());

            await Task.WhenAll(Enumerable.Range(0, 100).AsParallel().Select(i => storage.Put(new TestStorageObject("a"))));

            Assert.Equal(1, TestStorageProvider.InitializeCount);
        }

        [Fact]
        public async Task data_storage_can_handle_concurrent_requests()
        {
            for (var i = 0; i < 100; i++)
            {
                var storage = new MemoryStorage();
                var bag = new ConcurrentBag<Task>();
                Parallel.For(0, 1000, j =>
                {
                    bag.Add(storage.Put(new TestStorageObject(j)));
                });
                await Task.WhenAll(bag);
            }
        }
    }
}
