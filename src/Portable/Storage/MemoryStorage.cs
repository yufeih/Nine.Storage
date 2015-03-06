namespace Nine.Storage
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class MemoryStorage<T> : IStorage<T>, ICache<T> where T : class, IKeyed, new()
    {
        private readonly ConcurrentDictionary<string, T> items = new ConcurrentDictionary<string, T>();

        public bool TryGet(string key, out T value)
        {
            return items.TryGetValue(key, out value);
        }

        public void Put(string key, T value)
        {
            items.AddOrUpdate(key, value, (k, v) => value);
        }

        public bool Delete(string key)
        {
            T value;
            return items.TryRemove(key, out value);
        }

        public Task<T> Get(string key)
        {
            T result;
            return Task.FromResult(items.TryGetValue(key, out result) ? result : null);
        }

        public Task<IEnumerable<T>> Range(string minKey = null, string maxKey = null, int? count = null)
        {
            var result =
                from x in items
                where (minKey == null || string.CompareOrdinal(x.Key, minKey) >= 0) &&
                      (maxKey == null || string.CompareOrdinal(x.Key, maxKey) < 0)
                orderby x.Key
                select x.Value;

            if (count != null)
            {
                result = result.Take(count.Value);
            }

            return Task.FromResult<IEnumerable<T>>(result.ToArray());
        }

        public Task<bool> Add(T value)
        {
            return Task.FromResult(items.TryAdd(value.GetKey(), value));
        }

        public Task Put(T value)
        {
            Put(value.GetKey(), value);
            return Task.FromResult(0);
        }

        Task<bool> IStorage<T>.Delete(string key)
        {
            return Task.FromResult(Delete(key));
        }
    }

    public class MemoryStorage : Storage
    {
        public MemoryStorage() : base(typeof(MemoryStorage<>)) { }
    }
}
