namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Xunit;

    public class SyncSourceTest
    {
        public object MakeObservableStorage(Type type)
        {
            return typeof(SyncSourceTest).GetMethod("MakeObservableStorageCore").MakeGenericMethod(type).Invoke(null, null);
        }

        public static IStorage<T> MakeObservableStorageCore<T>() where T : class, IKeyed, new()
        {
            return new ObservableStorage<T>(new MemoryStorage<T>());
        }

        [Fact]
        public async Task subscribe_to_all_changes()
        {
            var changes = new List<Delta<TestStorageObject>>();
            var storage = new ObservableStorage<TestStorageObject>(new MemoryStorage<TestStorageObject>());

            storage.On(x => changes.Add(x));
            storage.On(x => changes.Add(x));
            await storage.Put(new TestStorageObject { Id = "b" });
            Assert.Equal(2, changes.Count);
            await storage.Delete(new TestStorageObject { Id = "b" });
            Assert.Equal(4, changes.Count);
        }

        [Fact]
        public async Task subscribe_to_a_single_item()
        {
            var changes = new List<Delta<TestStorageObject>>();
            var storage = new ObservableStorage<TestStorageObject>(new MemoryStorage<TestStorageObject>());

            storage.On("b", x => changes.Add(x));
            storage.On("b", x => changes.Add(x));
            await storage.Put(new TestStorageObject { Id = "b" });
            Assert.Equal(2, changes.Count);
            await storage.Put(new TestStorageObject { Id = "c" });
            Assert.Equal(2, changes.Count);
            await storage.Delete(new TestStorageObject { Id = "b" });
            Assert.Equal(4, changes.Count);
        }

        [Fact]
        public async Task subscribe_then_unsubscribe_using_dispose()
        {
            var changes = new List<Delta<TestStorageObject>>();
            var storage = new Storage(MakeObservableStorage);
            var subscription = storage.On<TestStorageObject>(x => changes.Add(x));

            await storage.Put(new TestStorageObject { Id = "a" });
            subscription.Dispose();
            await storage.Put(new TestStorageObject { Id = "b" });

            Assert.Equal(1, changes.Count);
        }

        [Fact]
        public async Task sync_to_storage_change()
        {
            var changeCount = 0;
            StringComparison? change = null;

            var storage = new Storage(MakeObservableStorage);
            var initial = StringComparison.InvariantCultureIgnoreCase;
            storage.On<TestStorageObject>("a", x => x.Enum, x => { change = x.Enum; changeCount++; });

            await Task.Delay(10);
            Assert.Equal(0, (int)change);
            Assert.Equal(1, changeCount);

            await storage.Put(new TestStorageObject("a") { Enum = initial });

            await Task.Delay(10);
            Assert.Equal(initial, change);
            Assert.Equal(2, changeCount);

            await storage.Put(new TestStorageObject("a") { Enum = initial });
            await Task.Delay(10);
            Assert.Equal(2, changeCount);

            for (int i = 0; i < 10; i++)
            {
                await storage.Put(new TestStorageObject("a") { Enum = StringComparison.InvariantCulture });
            }

            await Task.Delay(10);
            Assert.Equal(StringComparison.InvariantCulture, change);
            Assert.Equal(3, changeCount);
        }
    }
}
