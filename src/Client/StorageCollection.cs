namespace System.Collections.Specialized
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Nine.Storage;
    using Nine.Storage.Syncing;

    /// <summary>
    /// Represents a sorted observable collection that is loaded from a storage and optionally synced with a storage notification source.
    /// </summary>
    public class StorageCollection<T, TViewModel> : IReadOnlyList<TViewModel>, INotifyCollectionChanged, INotifyPropertyChanged, ISupportIncrementalLoading where T : IKeyed
    {
        struct Entry
        {
            public string Key;
            public TViewModel Value;
            public T Data;
        }

        private readonly SynchronizationContext _syncContext = SynchronizationContext.Current;
        private readonly List<Entry> _collection = new List<Entry>();
        private readonly Func<T, TViewModel, TViewModel> _convert;

        private readonly IStorage _storage;
        private readonly ISyncSource _observableStorage;
        private readonly List<Delta<T>> _pendingChanges = new List<Delta<T>>();

        private readonly string _minKey;
        private readonly string _maxKey;

        private bool _subscribed;
        private IDisposable _subscription;

        private string _cursor;

        public int Count { get { return _collection.Count; } }
        public TViewModel this[int index] { get { return _collection[index].Value; } }

        public IStorage Storage { get { return _storage; } }
        public string MinKey { get { return _minKey; } }
        public string MaxKey { get { return _maxKey; } }

        public bool IsLoading { get; private set; }
        public bool HasMoreItems { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public StorageCollection(IStorage storage, string prefix, Func<T, TViewModel> convert)
            : this(storage, prefix, (x, e) => convert(x))
        { }

        public StorageCollection(IStorage storage, string prefix, Func<T, TViewModel, TViewModel> convert)
            : this(storage, prefix, StorageKey.Increment(prefix), convert)
        { }

        public StorageCollection(IStorage storage, string minKey, string maxKey, Func<T, TViewModel> convert)
            : this(storage, minKey, maxKey, (x, e) => convert(x))
        { }

        public StorageCollection(IStorage storage, string minKey, string maxKey, Func<T, TViewModel, TViewModel> convert)
        {
            if (storage == null) throw new ArgumentException(nameof(storage));
            if (convert == null) throw new ArgumentException(nameof(convert));

            _storage = storage;
            _minKey = minKey;
            _maxKey = maxKey;
            _cursor = minKey;
            _convert = convert;

            HasMoreItems = true;

            _observableStorage = storage as ISyncSource;
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
                var loadedCount = await LoadMoreItemsCoreAsync(batchSize);
                if (loadedCount < batchSize)
                {
                    HasMoreItems = false;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasMoreItems)));
                }
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
                var requestCount = Math.Min(batchSize, count - total);
                var resultCount = await LoadMoreItemsCoreAsync(requestCount);
                if (resultCount <= 0) return total;
                if (resultCount < requestCount) return total + resultCount;
                total += resultCount;
            }
            return total;
        }

        private async Task<int> LoadMoreItemsCoreAsync(int count)
        {
            if (IsLoading || !HasMoreItems || count <= 0) return 0;

            try
            {
                if (_cursor != null && string.CompareOrdinal(_cursor, _maxKey) >= 0)
                {
                    HasMoreItems = false;
                    return 0;
                }

                IsLoading = true;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsLoading"));

                if (!_subscribed)
                {
                    _subscribed = true;

                    if (_observableStorage != null)
                    {
                        // If there are any actions happened before the subscription is set, thoses changes will be lost
                        _subscription = _observableStorage.On<T>(OnStorageChanged);
                    }
                }

                var items = await _storage.Range<T>(_cursor, _maxKey, count);
                var itemCount = items.Count();

                if (itemCount <= 0)
                {
                    HasMoreItems = false;
                    return 0;
                }

                var addedItems = new List<TViewModel>(itemCount);
                var originalCount = _collection.Count;
                var index = 0;

                foreach (var item in items)
                {
                    if (item == null) continue;

                    var key = item.GetKey();

                    if (string.CompareOrdinal(key, _cursor) > 0) _cursor = StorageKey.Increment(key);
                    if (TryFindIndex(key, out index)) continue;
                    var value = _convert(item, default(TViewModel));

                    _collection.Insert(index, new Entry { Key = key, Data = item, Value = value });
                    addedItems.Add(value);
                }

                var addedCount = _collection.Count - originalCount;
                if (addedCount > 0)
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, addedItems));
                }

                if (addedCount <= 0 || string.CompareOrdinal(_cursor, _maxKey) >= 0)
                {
                    HasMoreItems = false;
                }

                return addedCount;
            }
            finally
            {
                IsLoading = false;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoading)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasMoreItems)));

                foreach (var change in _pendingChanges)
                {
                    OnStorageChangedCore(change);
                }
                _pendingChanges.Clear();
            }
        }

        private void OnStorageChanged(Delta<T> change)
        {
            if ((_minKey != null && string.CompareOrdinal(change.Key, _minKey) < 0) ||
                (_maxKey != null && string.CompareOrdinal(change.Key, _maxKey) >= 0))
            {
                return;
            }

            if (_syncContext != null)
            {
                _syncContext.Post(c => OnStorageChangedCore((Delta<T>)c), change);
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
                _pendingChanges.Add(change);
                return;
            }

            var index = 0;
            var key = change.Key;

            if (change.Action == DeltaAction.Add)
            {
                if (!TryFindIndex(key, out index))
                {
                    var value = _convert(change.Value, default(TViewModel));
                    _collection.Insert(index, new Entry { Key = key, Data = change.Value, Value = value });

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, value, index));
                }
            }
            else if (change.Action == DeltaAction.Remove)
            {
                if (TryFindIndex(key, out index))
                {
                    var entry = _collection[index];
                    _collection.RemoveAt(index);

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, entry.Value, index));
                }
            }
            else if (change.Action == DeltaAction.Put)
            {
                if (TryFindIndex(key, out index))
                {
                    var entry = _collection[index];
                    var value = _convert(change.Value, entry.Value);
                    _collection[index] = new Entry { Key = key, Data = change.Value, Value = value };

                    if (!Equals(value, entry.Value))
                    {
                        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value, entry.Value, index));
                    }
                }
                else
                {
                    var value = _convert(change.Value, default(TViewModel));
                    _collection.Insert(index, new Entry { Key = key, Data = change.Value, Value = value });

                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, value, index));
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
            var right = _collection.Count - 1;

            while (left <= right)
            {
                var middle = left + (right - left) / 2;
                var comparison = string.CompareOrdinal(key, _collection[middle].Key);
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

        public IEnumerator<TViewModel> GetEnumerator()
        {
            return _collection.Select(x => x.Value).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    /// <summary>
    /// Represents a sorted observable collection that is loaded from a storage and optionally synced with a storage notification source.
    /// </summary>
    public class StorageCollection<T> : StorageCollection<T, T> where T : IKeyed
    {
        public StorageCollection(IStorage storage, string prefix)
            : base(storage, prefix, (x, e) => x)
        { }

        public StorageCollection(IStorage storage, string minKey, string maxKey)
            : base(storage, minKey, maxKey, (x, e) => x)
        { }

        public new StorageCollection<T> WithAllItems(int batchSize = 100)
        {
            return (StorageCollection<T>)base.WithAllItems(batchSize);
        }

        public new StorageCollection<T> WithItems(int count, int batchSize = 100)
        {
            return (StorageCollection<T>)base.WithItems(count, batchSize);
        }
    }
}