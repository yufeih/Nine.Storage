namespace Nine.Storage.Collections
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class StorageCollectionTest : IDisposable
    {
        private readonly UISynchronizationContext syncContext = UISynchronizationContext.BindToCurrent();

        [Fact]
        public async Task should_load_all_items()
        {
            var storage = new MemoryStorage();
            await Task.WhenAll(Enumerable.Range(0, 8).Select(x => storage.Put(new TestStorageObject(x))));

            var collection = new StorageCollection<TestStorageObject>(storage, "0", "6");
            Assert.True(collection.HasMoreItems);
            Assert.Equal(0, collection.Count);

            await collection.LoadAllAsync(2);

            Assert.Equal(6, collection.Count);
            Assert.False(collection.HasMoreItems);
            Assert.Equal(collection.Select(e => e.Id), Enumerable.Range(0, 6).Select(i => i.ToString()));
        }

        [Fact]
        public async Task should_load_incrementally_from_an_existing_storage()
        {
            var storage = new MemoryStorage();
            await Task.WhenAll(Enumerable.Range(0, 8).Select(x => storage.Put(new TestStorageObject(x))));

            var collection = new StorageCollection<TestStorageObject>(storage, "0", "6");
            Assert.True(collection.HasMoreItems);
            Assert.Equal(0, collection.Count);

            await collection.LoadMoreItemsAsync(2);

            Assert.Equal(2, collection.Count);
            Assert.Equal(collection.Select(e => e.Id), Enumerable.Range(0, 2).Select(i => i.ToString()));

            await collection.LoadMoreItemsAsync(2);

            Assert.Equal(4, collection.Count);
            Assert.Equal(collection.Select(e => e.Id), Enumerable.Range(0, 4).Select(i => i.ToString()));

            Assert.False(collection.IsLoading);
            Assert.True(collection.HasMoreItems);

            await collection.LoadMoreItemsAsync(10);
            Assert.Equal(6, collection.Count);
            Assert.False(collection.HasMoreItems);
            Assert.Equal(collection.Select(e => e.Id), Enumerable.Range(0, 6).Select(i => i.ToString()));
        }

        [Fact]
        public async Task should_sync_to_storage_change()
        {
            var changes = new List<NotifyCollectionChangedEventArgs>();
            var storage = new StorageContainer(x => new ObservableStorage<TestStorageObject>(new MemoryStorage<TestStorageObject>()));
            var collection = new StorageCollection<TestStorageObject>(storage, "0", "6");

            collection.CollectionChanged += (sender, e) => { lock (changes) { changes.Add(e); } };
            await collection.LoadMoreItemsAsync(10);

            await Task.Delay(20);
            Assert.Equal(0, changes.Count);

            await Task.WhenAll(Enumerable.Range(5, 3).Select(x => storage.Add(new TestStorageObject(x))));
            await Task.Delay(20);

            Assert.Equal(1, changes.Count);
            Assert.Equal(1, collection.Count);
            changes.Clear();

            await Task.WhenAll(Enumerable.Range(0, 8).Select(x => storage.Put(new TestStorageObject(x))));

            await Task.Delay(20);
            Assert.Equal(6, changes.Count);
            Assert.Equal(6, collection.Count);
            changes.Clear();

            await Task.WhenAll(Enumerable.Range(4, 4).Select(x => storage.Put(new TestStorageObject(x))));

            await Task.Delay(20);
            Assert.Equal(2, changes.Count);
            Assert.Equal(6, collection.Count);
            changes.Clear();

            await Task.WhenAll(Enumerable.Range(2, 6).Select(x => storage.Delete<TestStorageObject>(x.ToString())));

            await Task.Delay(20);
            Assert.Equal(4, changes.Count);
            Assert.Equal(2, collection.Count);
            changes.Clear();
        }

        [Fact]
        public async Task should_sync_to_concurrent_storage_changes()
        {
            var count = 100;
            var storage = new StorageContainer(x => new ObservableStorage<TestStorageObject>(new MemoryStorage<TestStorageObject>()));

            await Task.WhenAll(Enumerable.Range(0, count).AsParallel().Select(i => storage.Put(new TestStorageObject(i))));

            var collection = new StorageCollection<TestStorageObject>(storage, null, null).WithAllItems();

            await Task.WhenAll(Enumerable.Range(count, count).AsParallel().Select(i => storage.Put(new TestStorageObject(i))));
            await collection.ChangedTo(m => m.Select(x => x.Id).Distinct().Count() == count * 2, 10000);
        }

        [Fact]
        public void weak_reference_can_be_released_by_gc()
        {
            // http://stackoverflow.com/questions/578967/how-can-i-write-a-unit-test-to-determine-whether-an-object-can-be-garbage-collec
            WeakReference reference = null;
            new Action(() =>
            {
                var obj = new object();
                reference = new WeakReference(obj, true);
            })();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.Null(reference.Target);

            var obj2 = new object();
            new Action(() =>
            {
                reference = new WeakReference(obj2, true);
            })();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.NotNull(reference.Target);
            Assert.NotNull(obj2);
        }

        [Fact]
        public async Task should_be_unsubscribed_after_gc()
        {
            var changes = new List<NotifyCollectionChangedEventArgs>();
            var storage = new StorageContainer(x => new ObservableStorage<TestStorageObject>(new MemoryStorage<TestStorageObject>()));
            var collection = new StorageCollection<TestStorageObject>(storage, "0", "6").WithAllItems();
            var handler = new NotifyCollectionChangedEventHandler((sender, e) => { changes.Add(e); });

            collection.CollectionChanged += handler;

            await storage.Add(new TestStorageObject(0));
            await Task.Delay(20);
            Assert.Equal(1, changes.Count);

            GC.Collect();
            GC.WaitForPendingFinalizers();

            await storage.Add(new TestStorageObject(1));
            await Task.Delay(20);
            Assert.Equal(2, changes.Count);

            collection.CollectionChanged -= handler;
            collection = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();

            await storage.Add(new TestStorageObject(3));
            await Task.Delay(20);
            Assert.Equal(2, changes.Count);
        }

        public void Dispose()
        {
            syncContext.Dispose();
        }
    }
}
