namespace Nine.Storage
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a sorted observable collection that is loaded from a storage and optionally synced with a storage notification source.
    /// </summary>
    public class StorageCollection<T, TViewModel> : IReadOnlyList<TViewModel>, INotifyCollectionChanged, INotifyPropertyChanged, ISupportIncrementalLoading where T : class, IKeyed, new()
    {
        struct Entry
        {
            public string Key;
            public TViewModel Value;
            public T Data;
        }

        private readonly SynchronizationContext syncContext = SynchronizationContext.Current;
        private readonly List<Entry> collection = new List<Entry>();
        private readonly Func<T, TViewModel, TViewModel> convert;

        private IStorage storage;
        private readonly ISyncSource observableStorage;
        private readonly List<Delta<T>> pendingChanges = new List<Delta<T>>();

        private readonly string minKey;
        private readonly string maxKey;

        private bool subscribed;
        private IDisposable subscription;

        private string cursor;

        public int Count { get { return collection.Count; } }
        public TViewModel this[int index] { get { return collection[index].Value; } }

        public IStorage Storage { get { return storage; } }
        public string MinKey { get { return minKey; } }
        public string MaxKey { get { return maxKey; } }

        public bool IsLoading { get; private set; }
        public bool HasMoreItems { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public StorageCollection(IStorage storage, string prefix, Func<T, TViewModel, TViewModel> convert)
            : this(storage, prefix, StorageKey.Increment(prefix), convert)
        { }

        public StorageCollection(IStorage storage, string minKey, string maxKey, Func<T, TViewModel, TViewModel> convert)
        {
            if (storage == null) throw new ArgumentException("storage");
            if (convert == null) throw new ArgumentException("convert");

            this.storage = storage;
            this.minKey = minKey;
            this.maxKey = maxKey;
            this.cursor = minKey;
            this.convert = convert;

            this.HasMoreItems = true;

            this.observableStorage = storage as ISyncSource;
        }

#pragma warning disable CS4014
        public StorageCollection<T, TViewModel> WithAllItems(int batchSize = 100)
        {
            LoadAllAsync(batchSize);
            return this;
        }

        public StorageCollection<T, TViewModel> WithItems(int count, int batchSize = 100)
        {
            LoadMoreItemsAsync(count);
            return this;
        }
#pragma warning restore CS4014

        public async Task LoadAllAsync(int batchSize = 100)
        {
            while (HasMoreItems)
            {
                await LoadMoreItemsCoreAsync(batchSize);
            }
        }

        public Task<int> LoadMoreItemsAsync(int count)
        {
            return LoadMoreItemsAsync(count, 100);
        }

        public async Task<int> LoadMoreItemsAsync(int count, int batchSize)
        {
            var total = 0;
            while (total < count)
            {
                var current = await LoadMoreItemsCoreAsync(Math.Min(batchSize, count - total));
                if (current <= 0) return total;
                total += current;
            }
            return total;
        }

        private async Task<int> LoadMoreItemsCoreAsync(int count)
        {
            if (!subscribed)
            {
                subscribed = true;

                if (observableStorage != null)
                {
                    // If there are any actions happened before the subscription is set, thoses changes will be lost
                    subscription = observableStorage.On<T>(minKey, maxKey, OnStorageChanged);
                }
            }

            if (IsLoading || !HasMoreItems || count <= 0) return 0;

            if (cursor != null && string.CompareOrdinal(cursor, maxKey) >= 0)
            {
                HasMoreItems = false;
                return 0;
            }

            try
            {
                IsLoading = true;
                OnPropertyChanged("IsLoading");

                var items = await storage.Range<T>(cursor, maxKey, count);
                var itemCount = items.Count();
                if (itemCount <= 0)
                {
                    HasMoreItems = false;
                    return 0;
                }

                var addedItems = new List<TViewModel>(itemCount);
                var originalCount = collection.Count;
                var index = 0;

                foreach (var item in items)
                {
                    if (item == null) continue;

                    var key = item.GetKey();
                    if (string.CompareOrdinal(key, cursor) > 0) cursor = key;
                    if (TryFindIndex(key, out index)) continue;
                    var value = convert(item, default(TViewModel));

                    collection.Insert(index, new Entry { Key = key, Data = item, Value = value });
                    addedItems.Add(value);
                }

                var addedCount = collection.Count - originalCount;
                if (addedCount > 0)
                {
                    OnPropertyChanged("Count");
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, addedItems));
                }

                if (addedCount <= 0 || itemCount < count || string.CompareOrdinal(cursor, maxKey) >= 0)
                {
                    HasMoreItems = false;
                }

                return addedCount;
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged("IsLoading");
                OnPropertyChanged("HasMoreItems");

                foreach (var change in pendingChanges)
                {
                    OnStorageChanged(change);
                }
                pendingChanges.Clear();
            }
        }

        private void OnStorageChanged(Delta<T> change)
        {
            if (syncContext != null)
            {
                syncContext.Post(c => OnStorageChangedCore((Delta<T>)c), change);
            }
            else
            {
                OnStorageChangedCore(change);
            }
        }

        private void OnStorageChangedCore(Delta<T> change)
        {
            if (IsLoading)
            {
                pendingChanges.Add(change);
                return;
            }

            var index = 0;
            var key = change.Key;

            if (change.Action == DeltaAction.Add)
            {
                if (!TryFindIndex(key, out index))
                {
                    var value = convert(change.Value, default(TViewModel));
                    collection.Insert(index, new Entry { Key = key, Data = change.Value, Value = value });

                    OnPropertyChanged("Count");
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, value, index));
                }
            }
            else if (change.Action == DeltaAction.Remove)
            {
                if (TryFindIndex(key, out index))
                {
                    var entry = collection[index];
                    collection.RemoveAt(index);

                    OnPropertyChanged("Count");
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, entry.Value, index));
                }
            }
            else if (change.Action == DeltaAction.Put)
            {
                if (TryFindIndex(key, out index))
                {
                    var entry = collection[index];
                    var value = convert(change.Value, entry.Value);
                    collection[index] = new Entry { Key = key, Data = change.Value, Value = value };
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value, entry.Value, index));
                }
                else
                {
                    var value = convert(change.Value, default(TViewModel));
                    collection.Insert(index, new Entry { Key = key, Data = change.Value, Value = value });

                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, value, index));
                }
            }
            else
            {
                throw new NotSupportedException(string.Format("StorageAction {0} not supported", change.Action));
            }
        }

        private bool TryFindIndex(string key, out int index)
        {
            var left = 0;
            var right = collection.Count - 1;

            while (left <= right)
            {
                var middle = left + (right - left) / 2;
                var comparison = string.CompareOrdinal(key, collection[middle].Key);
                if (comparison == 0)
                {
                    index = middle;
                    return true;
                }

                if (comparison < 0)
                    right = middle - 1;
                else
                    left = middle + 1;
            }

            index = left;
            return false;
        }

        private void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            var collectionChanged = CollectionChanged;
            if (collectionChanged != null)
            {
                collectionChanged(this, e);
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            var propertyChanged = PropertyChanged;
            if (propertyChanged != null)
            {
                propertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public IEnumerator<TViewModel> GetEnumerator()
        {
            return collection.Select(x => x.Value).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    /// <summary>
    /// Represents a sorted observable collection that is loaded from a storage and optionally synced with a storage notification source.
    /// </summary>
    public class StorageCollection<T> : StorageCollection<T, T> where T : class, IKeyed, new()
    {
        public StorageCollection(IStorage storage, string prefix)
            : base(storage, prefix, (x, e) => x)
        { }

        public StorageCollection(IStorage storage, string minKey, string maxKey)
            : base(storage, minKey, maxKey, (x, e) => x)
        { }
    }
}