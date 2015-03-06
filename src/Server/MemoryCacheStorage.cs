namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Caching;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a single instance memory cached storage system that is based on System.Runtime.Caching.MemoryCache.
    /// </summary>
    public class MemoryCacheStorage<T> : IStorage<T>, ICache<T>, IDisposable where T : class, IKeyed, new()
    {
        /// <summary>
        /// Since MemoryCache does not support null value, this constant identifies values that does
        /// not exist in the persisted storage.
        /// </summary>
        private static readonly object emptyObject = new object();

        private readonly CacheItemPolicy policy = new CacheItemPolicy { SlidingExpiration = TimeSpan.FromMinutes(30) };
        private readonly MemoryCache memoryCache = new MemoryCache("MCS-" + typeof(T).Name);

        public TimeSpan SlidingExpiration
        {
            get { return policy.SlidingExpiration; }
            set { policy.SlidingExpiration = value; }
        }

        public MemoryCacheStorage() { }

        public bool TryGet(string key, out T value)
        {
            var result = memoryCache.Get(key);
            if (result == emptyObject) { value = null; return true; }
            if (result == null) { value = null; return false; }

            value = (T)result;
            return true;
        }

        public Task<T> Get(string key)
        {
            T result;
            TryGet(key, out result);
            return Task.FromResult(result);
        }

        public void Put(string key, T value)
        {
            memoryCache.Set(key, value ?? emptyObject, policy);
        }

        public bool Delete(string key)
        {
            return memoryCache.Remove(key) != null;
        }

        public Task<IEnumerable<T>> Range(string minKey, string maxKey, int? maxCount)
        {
            throw new NotSupportedException();
        }

        public Task<bool> Add(T value)
        {
            return Task.FromResult(memoryCache.Add(value.GetKey(), value ?? emptyObject, policy));
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                memoryCache.Dispose();
            }
        }
    }
}
