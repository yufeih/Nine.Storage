namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Xunit;

    public class ObservableStorageTest : StorageSpec<ObservableStorageTest>
    {
        public override IEnumerable<ITestFactory<IStorage<TestStorageObject>>> GetData()
        {
            return new[]
            {
                new TestFactory<IStorage<TestStorageObject>>(typeof(ObservableStorage<>), () => new ObservableStorage<TestStorageObject>(new MemoryStorage<TestStorageObject>())),
                new TestFactory<IStorage<TestStorageObject>>(typeof(ObservableStorage<>), () => new ObservableStorage<TestStorageObject>(new MemoryStorage<TestStorageObject>(), true)),
            };
        }

        [Fact]
        public async Task it_should_reused_existing_instance()
        {
            var storage = new ObservableStorage<TestStorageObject>(new PersistedStorage<TestStorageObject>(), true);

            await storage.Put(new TestStorageObject("id") { Name = "1" });
            var instance = await storage.Get("id");
            Assert.Equal("1", instance.Name);
            await storage.Put(new TestStorageObject("id") { Name = "2" });
            Assert.Equal("2", instance.Name);
            Assert.True(ReferenceEquals(instance, await storage.Get("id")));
            
            Assert.Collection(await storage.Range(null, null, null),
                e => Assert.True(ReferenceEquals(instance, e)));

            await storage.Delete("id");
            await storage.Put(new TestStorageObject("id") { Name = "3" });
            Assert.Equal("2", instance.Name);
            Assert.False(ReferenceEquals(instance, await storage.Get("id")));
        }
    }
}
