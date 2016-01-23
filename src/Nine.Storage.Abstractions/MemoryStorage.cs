namespace Nine.Storage
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Nine.Storage.Caching;

    public class MemoryStorage<T> : IStorage<T>, ICache<T>
    {
        private readonly bool weak;
        private readonly ConcurrentDictionary<string, Entry> items = new ConcurrentDictionary<string, Entry>();

        public MemoryStorage() { }
        public MemoryStorage(bool useWeakReference)
        {
            this.weak = useWeakReference && !typeof(T).GetTypeInfo().IsValueType;
        }

        public bool TryGet(string key, out T value)
        {
            Entry entry;
            value = default(T);
            return items.TryGetValue(key, out entry) && entry.TryGetValue(out value);
        }

        public void Put(string key, T value)
        {
            items.AddOrUpdate(key, new Entry(value, weak), (k, v) => new Entry(value, weak));
        }

        public bool Delete(string key)
        {
            Entry value;
            return items.TryRemove(key, out value);
        }

        public Task<T> Get(string key)
        {
            T result;
            return Task.FromResult(TryGet(key, out result) ? result : default(T));
        }

        public Task<IEnumerable<T>> Range(string minKey = null, string maxKey = null, int? count = null)
        {
            var result =
                from x in items
                where (minKey == null || string.CompareOrdinal(x.Key, minKey) >= 0) &&
                      (maxKey == null || string.CompareOrdinal(x.Key, maxKey) < 0)
                let value = x.Value.Value
                where value != null
                orderby x.Key
                select value;

            if (count != null)
            {
                result = result.Take(count.Value);
            }

            return Task.FromResult<IEnumerable<T>>(result.ToArray());
        }

        public Task<bool> Add(string key, T value)
        {
            return Task.FromResult(items.TryAdd(key, new Entry(value, weak)));
        }

        Task IStorage<T>.Put(string key, T value)
        {
            Put(key, value);
            return Task.FromResult(0);
        }

        Task<bool> IStorage<T>.Delete(string key)
        {
            return Task.FromResult(Delete(key));
        }

        struct Entry
        {
            private readonly T value;
            private readonly WeakReference weakValue;

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
                    this.value = default(T);
                    this.weakValue = new WeakReference(value);
                }
                else
                {
                    this.value = value;
                    this.weakValue = null;
                }
            }

            public bool TryGetValue(out T value)
            {
                if (weakValue != null)
                {
                    object obj = weakValue.Target;
                    if (obj != null && obj is T)
                    {
                        value = (T)obj;
                        return true;
                    }
                }

                value = this.value;
                return true;
            }
        }
    }

    public class MemoryStorage : Storage
    {
        public MemoryStorage() : base(typeof(MemoryStorage<>)) { }
    }
}
