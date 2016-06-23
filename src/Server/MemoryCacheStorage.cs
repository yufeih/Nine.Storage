namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Caching;
    using System.Threading.Tasks;
    using Nine.Storage.Caching;

    /// <summary>
    /// Represents a single instance memory cached storage system that is based on System.Runtime.Caching.MemoryCache.
    /// </summary>
    public class MemoryCacheStorage<T> : IStorage<T>, ICache<T>, IDisposable where T : class
    {
        /// <summary>
        /// Since MemoryCache does not support null value, this constant identifies values that does
        /// not exist in the persisted storage.
        /// </summary>
        private static readonly object EmptyObject = new object();

        private readonly CacheItemPolicy _policy = new CacheItemPolicy { SlidingExpiration = TimeSpan.FromMinutes(30) };
        private readonly MemoryCache _memoryCache = new MemoryCache("MCS-" + typeof(T).Name);

        public TimeSpan SlidingExpiration
        {
            get { return _policy.SlidingExpiration; }
            set { _policy.SlidingExpiration = value; }
        }

        public MemoryCacheStorage() { }

        public bool TryGet(string key, out T value)
        {
            var result = _memoryCache.Get(key);
            if (result == EmptyObject) { value = null; return true; }
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
            _memoryCache.Set(key, value ?? EmptyObject, _policy);
        }

        public bool Delete(string key)
        {
            return _memoryCache.Remove(key) != null;
        }

        public Task<IEnumerable<T>> Range(string minKey, string maxKey, int? maxCount)
        {
            throw new NotSupportedException();
        }

        public Task<bool> Add(string key, T value)
        {
            return _memoryCache.Add(key, value ?? EmptyObject, _policy) ? Tasks.True : Tasks.False;
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _memoryCache.Dispose();
            }
        }
    }
}
