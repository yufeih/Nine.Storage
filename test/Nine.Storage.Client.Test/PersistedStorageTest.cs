namespace Nine.Storage
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Xunit;

    public class PersistedStorageTest : StorageSpec<PersistedStorageTest>
    {
        public override IEnumerable<ITestFactory<IStorage<TestStorageObject>>> GetData()
        {
            return new[]
            {
                new TestFactory<IStorage<TestStorageObject>>(typeof(PersistedStorage<>), () => new PersistedStorage<TestStorageObject>(Guid.NewGuid().ToString())),
            };
        }

        [Fact]
        public async Task persisted_storage_put_and_get()
        {
            var storage = new PersistedStorage<TestStorageObject>(Guid.NewGuid().ToString());
            await storage.Put(new TestStorageObject("a"));
            Assert.NotNull(await storage.Get("a"));

            var gets = new ConcurrentBag<TestStorageObject>();
            Parallel.For(0, 100, i =>
            {
                if (i % 2 == 0)
                {
                    storage.Put(new TestStorageObject("a")).Wait();
                }
                else
                {
                    gets.Add(storage.Get("a").Result);
                }
            });
            Assert.All(gets, got => Assert.NotNull(got));
        }
    }
}
