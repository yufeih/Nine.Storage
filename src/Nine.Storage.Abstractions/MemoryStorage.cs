namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading.Tasks;
    using Nine.Storage.Caching;

    public class MemoryStorage<T> : IStorage<T>, ICache<T>
    {
        private readonly bool _weak;
        private readonly SortedDictionary<string, Entry> _items = new SortedDictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        public MemoryStorage() { }
        public MemoryStorage(bool useWeakReference)
        {
            _weak = useWeakReference && !typeof(T).GetTypeInfo().IsValueType;
        }

        public bool TryGet(string key, out T value)
        {
            lock (_items)
            {
                Entry entry;
                value = default(T);
                return _items.TryGetValue(key, out entry) && entry.TryGetValue(out value);
            }
        }

        public void Put(string key, T value)
        {
            lock (_items)
            {
                _items[key] = new Entry(value, _weak);
            }
        }

        public bool Delete(string key)
        {
            lock (_items)
            {
                return _items.Remove(key);
            }
        }

        public Task<T> Get(string key)
        {
            lock (_items)
            {
                T result;
                return Task.FromResult(TryGet(key, out result) ? result : default(T));
            }
        }

        public Task<IEnumerable<T>> Range(string minKey = null, string maxKey = null, int? count = null)
        {
            T value;
            var result = new List<T>();

            lock (_items)
            {
                foreach (var pair in _items)
                {
                    if (minKey != null && string.CompareOrdinal(pair.Key, minKey) < 0) continue;
                    if (maxKey != null && string.CompareOrdinal(pair.Key, maxKey) >= 0) break;
                    if (pair.Value.TryGetValue(out value)) result.Add(value);
                    if (count.HasValue && result.Count >= count) break;
                }
            }
            return Task.FromResult<IEnumerable<T>>(result);
        }

        public Task<bool> Add(string key, T value)
        {
            lock (_items)
            {
                if (_items.ContainsKey(key)) return CommonTasks.False;
                _items.Add(key, new Entry(value, _weak));
                return CommonTasks.True;
            }
        }

        Task IStorage<T>.Put(string key, T value)
        {
            Put(key, value);
            return Task.CompletedTask;
        }

        Task<bool> IStorage<T>.Delete(string key)
        {
            return Delete(key) ? CommonTasks.True : CommonTasks.False;
        }

        struct Entry
        {
            private readonly T _value;
            private readonly WeakReference _weakValue;

            public T Value
            {
                get
                {
                    T result;
                    return TryGetValue(out result) ? result : default(T);
                }
            }

            public Entry(T value, bool weak)
            {
                if (weak)
                {
                    _value = default(T);
                    _weakValue = new WeakReference(value);
                }
                else
                {
                    _value = value;
                    _weakValue = null;
                }
            }

            public bool TryGetValue(out T value)
            {
                if (_weakValue != null)
                {
                    object obj = _weakValue.Target;
                    if (obj != null && obj is T)
                    {
                        value = (T)obj;
                        return true;
                    }
                }

                value = _value;
                return true;
            }
        }
    }

    public class MemoryStorage : StorageContainer
    {
        public MemoryStorage() : base(typeof(MemoryStorage<>)) { }
    }
}
