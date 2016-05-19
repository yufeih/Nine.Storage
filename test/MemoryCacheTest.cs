namespace Nine.Storage.Caching
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Xunit;

    public class MemoryCacheStorageTest : StorageSpec<MemoryCacheStorageTest>
    {
        public override IEnumerable<ITestFactory<IStorage<TestStorageObject>>> GetData()
        {
            return new[]
            {
                new TestFactory<IStorage<TestStorageObject>>(typeof(MemoryCacheStorage<>), () => new MemoryCacheStorage<TestStorageObject>()),
                new TestFactory<IStorage<TestStorageObject>>(typeof(CachedStorage<>), () => new CachedStorage<TestStorageObject>(new MemoryCacheStorage<TestStorageObject>() , new MemoryCacheStorage<TestStorageObject>())),
            };
        }
    }

    public class MemoryCacheStorageCacheTest : CacheSpec<MemoryCacheStorageCacheTest>
    {
        public override IEnumerable<ITestFactory<ICache<TestStorageObject>>> GetData()
        {
            yield return new TestFactory<ICache<TestStorageObject>>(typeof(MemoryCacheStorage<>), () => new MemoryCacheStorage<TestStorageObject>());
        }

        [Fact]
        public async Task should_cache_null_values()
        {
            var persisted = new MemoryStorage<TestStorageObject>();
            var storage = new CachedStorage<TestStorageObject>(persisted, new MemoryCacheStorage<TestStorageObject>());
            var missCount = 0;
            storage.Missed += i => missCount++;

            for (int i = 0; i < 100; i++)
            {
                await storage.Get("1");
            }

            Assert.Equal(1, missCount);
        }

        [Fact]
        public async Task add_should_not_respect_cache()
        {
            var storage = new CachedStorage<TestStorageObject>(new MemoryStorage<TestStorageObject>(), new MemoryCacheStorage<TestStorageObject>());
            await storage.Get("1");
            Assert.True(await storage.Add(new TestStorageObject("1")));
        }
    }
}
