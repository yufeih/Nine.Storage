namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Xunit;

    public class CachedStorageTest : StorageSpec<CachedStorageTest>
    {
        public override IEnumerable<ITestFactory<IStorage<TestStorageObject>>> GetData()
        {
            return new[]
            {
                new TestFactory<IStorage<TestStorageObject>>(nameof(MemoryStorage), () => new MemoryStorage<TestStorageObject>()),
                new TestFactory<IStorage<TestStorageObject>>(typeof(CachedStorage<>), () => new CachedStorage<TestStorageObject>(new MemoryStorage<TestStorageObject>(), new MemoryStorage<TestStorageObject>())),
                new TestFactory<IStorage<TestStorageObject>>(typeof(CachedStorage<>), () => new CachedStorage<TestStorageObject>(new MemoryStorage<TestStorageObject>(), new MemoryStorage<TestStorageObject>()) { ReuseExistingInstance = true }),
                new TestFactory<IStorage<TestStorageObject>>(typeof(CachedStorage<>), () => new CachedStorage<TestStorageObject>(new MemoryStorage<TestStorageObject>(), new MemoryStorage<TestStorageObject>(), new MemoryStorage<CachedStorageItems<TestStorageObject>>())),
            };
        }

        [Fact]
        public async Task it_should_reused_existing_instance()
        {
            var storage = new CachedStorage<TestStorageObject>(new PersistedStorage<TestStorageObject>(), new MemoryStorage<TestStorageObject>(), new MemoryStorage<CachedStorageItems<TestStorageObject>>())
            {
                ReuseExistingInstance = true
            };

            await storage.Put(new TestStorageObject("id") { Name = "1" });
            var instance = await storage.Get("id");
            Assert.Equal("1", instance.Name);
            await storage.Put(new TestStorageObject("id") { Name = "2" });
            Assert.Equal("2", instance.Name);
            Assert.True(ReferenceEquals(instance, await storage.Get("id")));

            // TODO: Test range
            // await storage.Range(null, null, null);

            await storage.Delete("id");
            await storage.Put(new TestStorageObject("id") { Name = "3" });
            Assert.Equal("2", instance.Name);
            Assert.False(ReferenceEquals(instance, await storage.Get("id")));
        }
    }
}
