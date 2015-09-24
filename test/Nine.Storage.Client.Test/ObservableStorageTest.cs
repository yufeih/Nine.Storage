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
        public async Task it_should_reused_existing_instance_for_get()
        {
            var persisted = new MemoryStorage<TestStorageObject>();
            var storage = new ObservableStorage<TestStorageObject>(persisted, true);

            await persisted.Put(new TestStorageObject("1"));
            Assert.True(ReferenceEquals(
                await storage.Get<TestStorageObject>("1"),
                await storage.Get<TestStorageObject>("1")));

            await persisted.Put(new TestStorageObject("2"));
            await persisted.Put(new TestStorageObject("3"));

            var r1 = await storage.Range(null, null);
            var r2 = await storage.Range(null, null);

            Assert.Equal(r1, r2);
        }

        [Fact]
        public async Task it_should_reused_existing_instance_for_put()
        {
            var persisted = new MemoryStorage<TestStorageObject>();
            var storage = new ObservableStorage<TestStorageObject>(persisted, true);

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

        [Fact]
        public async Task it_should_notify_the_deleted_value()
        {
            Delta<TestStorageObject> delta = new Delta<TestStorageObject>();
            var storage = new ObservableStorage<TestStorageObject>(new MemoryStorage<TestStorageObject>(), true);
            await storage.Put(new TestStorageObject("id") { Name = "1" });

            storage.On(d => delta = d);

            await storage.Delete("id");

            Assert.Equal(DeltaAction.Remove, delta.Action);
            Assert.Equal("1", delta.Value.Name);
            Assert.Equal("id", delta.Value.Id);
        }
    }
}
