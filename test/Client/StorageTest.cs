namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Xunit;

    public class StorageTest : StorageSpec<StorageTest>
    {
        public override IEnumerable<ITestFactory<IStorage<TestStorageObject>>> GetData()
        {
            return new[]
            {
                new TestFactory<IStorage<TestStorageObject>>(nameof(MemoryStorage), () => new MemoryStorage<TestStorageObject>()),
                new TestFactory<IStorage<TestStorageObject>>(typeof(CachedStorage<>), () => new CachedStorage<TestStorageObject>(new MemoryStorage<TestStorageObject>(), new MemoryStorage<TestStorageObject>())),
                new TestFactory<IStorage<TestStorageObject>>(typeof(CachedStorage<>), () => new CachedStorage<TestStorageObject>(new MemoryStorage<TestStorageObject>(), new MemoryStorage<TestStorageObject>(), new MemoryStorage<CachedStorageItems<TestStorageObject>>())),
            };
        }

        [Fact]
        public async Task it_should_create_a_new_instance_on_put()
        {
            TestStorageObject a = null, b = null, c = null;

            var storage = new MemoryStorage();
            await storage.Put<TestStorageObject>("1", p => { a = p; p.Id = "1"; });
            await storage.Put<TestStorageObject>("2", p => { b = p; p.Id = "2"; });
            await storage.Put<TestStorageObject>("2", p => { c = p; });

            Assert.NotNull(a);
            Assert.NotEqual(a, b);
            Assert.Equal(b, c);
        }
    }
}
