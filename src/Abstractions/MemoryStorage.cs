namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Nine.Storage.Caching;

    public class MemoryStorage<T> : IStorage<T>, ICache<T>
    {
        private static readonly StringComparer s_comparer = StringComparer.OrdinalIgnoreCase;

        private readonly bool _weak;
        private readonly List<string> _keys = new List<string>();
        private readonly List<Entry> _values = new List<Entry>();

        public MemoryStorage() { }
        public MemoryStorage(bool useWeakReference)
        {
            _weak = useWeakReference && !typeof(T).GetTypeInfo().IsValueType;
        }

        public bool TryGet(string key, out T value)
        {
            lock (_values)
            {
                var i = _keys.BinarySearch(key, s_comparer);
                if (i >= 0)
                {
                    return _values[i].TryGetValue(out value);
                }
                else
                {
                    value = default(T);
                    return false;
                }
            }
        }

        public void Put(string key, T value)
        {
            lock (_values)
            {
                var i = _keys.BinarySearch(key, s_comparer);
                if (i >= 0)
                {
                    _keys[i] = key;
                    _values[i] = new Entry(value, _weak);
                }
                else
                {
                    var index = ~i;
                    _keys.Insert(index, key);
                    _values.Insert(index, new Entry(value, _weak));
                }
            }
        }

        public Task<bool> Add(string key, T value)
        {
            lock (_values)
            {
                var i = _keys.BinarySearch(key, s_comparer);
                if (i >= 0) return Tasks.False;
                var index = ~i;
                _keys.Insert(index, key);
                _values.Insert(index, new Entry(value, _weak));
                return Tasks.True;
            }
        }

        public bool Delete(string key)
        {
            lock (_values)
            {
                var i = _keys.BinarySearch(key, s_comparer);
                if (i >= 0)
                {
                    _keys.RemoveAt(i);
                    _values.RemoveAt(i);
                    return true;
                }
                return false;
            }
        }

        public Task<T> Get(string key)
        {
            lock (_values)
            {
                T result;
                return Task.FromResult(TryGet(key, out result) ? result : default(T));
            }
        }

        public Task<IEnumerable<T>> Range(string minKey = null, string maxKey = null, int? count = null)
        {
            T value;

            lock (_values)
            {
                var start = 0;
                var end = _keys.Count;
                if (minKey != null)
                {
                    start = _keys.BinarySearch(minKey, s_comparer);
                    if (start < 0) start = ~start;
                }
                if (maxKey != null)
                {
                    end = _keys.BinarySearch(maxKey, s_comparer);
                    if (end < 0) end = ~end;
                }

                if (end < start) return Task.FromResult(Enumerable.Empty<T>());

                var result = new List<T>();

                for (var i = start; i < end; i++)
                {
                    if (_values[i].TryGetValue(out value)) result.Add(value);
                    if (count.HasValue && result.Count >= count) break;
                }

                return Task.FromResult<IEnumerable<T>>(result);
            }
        }

        Task IStorage<T>.Put(string key, T value)
        {
            Put(key, value);
            return Tasks.Completed;
        }

        Task<bool> IStorage<T>.Delete(string key)
        {
            return Delete(key) ? Tasks.True : Tasks.False;
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
