namespace Nine.Storage.Caching
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Contains status about memory cached storage.
    /// </summary>
    public static class CachedStorageStatus
    {
        internal static long _missedCount = 0;
        internal static long _totalCount = 0;

        public static double HitRate => _totalCount > 0 ? 1.0 * (_totalCount - _missedCount) / _totalCount : 0.0;
    }
    
    public class CachedStorageItems<T> : IKeyed
    {
        public T[] Items { get; set; }
        public string Key { get; set; }
        public DateTime Time { get; set; }

        public string GetKey() => Key;
    }

    /// <summary>
    /// Represents a storage where objects are cached in the cache storage.
    /// </summary>
    public class CachedStorage<T> : IStorage<T>
    {
        /// <summary>
        /// Since MemoryCache does not support null value, this constant identifies values that does
        /// not exist in the persisted storage.
        /// </summary>
        private static readonly object EmptyObject = new object();

        private readonly IStorage<T> _persistStorage;
        private readonly ICache<T> _cache;
        private readonly ICache<CachedStorageItems<T>> _rangeCache;

        public event Action<string> Missed;

        public CachedStorage(IStorage<T> persistedStorage, ICache<T> cache, ICache<CachedStorageItems<T>> rangeCache = null)
        {
            if (persistedStorage == null) throw new ArgumentNullException("persistedStorage");
            if (cache == null) throw new ArgumentNullException("cache");

            _persistStorage = persistedStorage;
            _cache = cache;
            _rangeCache = rangeCache;
        }

        /// <summary>
        /// Gets an unique key value pair based on the specified key. Returns null if the key is not found.
        /// </summary>
        public async Task<T> Get(string key)
        {
            IncrementTotalCount();

            T result;
            if (_cache.TryGet(key, out result)) return result;

            IncrementMissedCount(key);

            var persisted = await _persistStorage.Get(key).ConfigureAwait(false);
            _cache.Put(key, persisted);
            return persisted;
        }

        /// <summary>
        /// Gets a list of key value pairs whose keys are inside the specified range.
        /// </summary>
        public async Task<IEnumerable<T>> Range(string minKey, string maxKey, int? maxCount)
        {
            IncrementTotalCount();

            CachedStorageItems<T> items;
            var canCache = (_rangeCache != null && maxCount == null && StorageKey.IsIncrement(minKey, maxKey));
            var cacheKey = canCache ? (minKey ?? "") : null;
            if (cacheKey != null && _rangeCache.TryGet(cacheKey, out items) && items != null)
            {
                return items.Items;
            }

            IncrementMissedCount(cacheKey);

            var persisted = (await _persistStorage.Range(minKey, maxKey, maxCount).ConfigureAwait(false)).ToArray();
            if (cacheKey != null)
            {
                _rangeCache.Put(cacheKey, new CachedStorageItems<T> { Items = persisted, Key = minKey });
            }
            return persisted;
        }

        private void IncrementTotalCount()
        {
            if (Interlocked.Increment(ref CachedStorageStatus._totalCount) > 100000)
            {
                Interlocked.Exchange(ref CachedStorageStatus._totalCount, 1);
                Interlocked.Exchange(ref CachedStorageStatus._missedCount, 0);
            }
        }

        private void IncrementMissedCount(string key)
        {
            Interlocked.Increment(ref CachedStorageStatus._missedCount);

            var missed = Missed;
            if (missed != null) missed(key);
        }

        /// <summary>
        /// Adds a new key value to the storage if the key does not already exist.
        /// </summary>
        public async Task<bool> Add(string key, T value)
        {
            if (await _persistStorage.Add(key, value).ConfigureAwait(false))
            {
                // SHOULD override existing cache using Put !!!
                _cache.Put(key, value);

                if (_rangeCache != null) InvalidateRange(key);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Adds a key value pair to the storage if the key does not already exist,
        /// or updates a key value pair in the storage if the key already exists.
        /// </summary>
        public async Task Put(string key, T value)
        {
            await _persistStorage.Put(key, value).ConfigureAwait(false);
            _cache.Put(key, value);
            if (_rangeCache != null) InvalidateRange(key);
        }

        /// <summary>
        /// Permanently removes the value with the specified key.
        /// </summary>
        public async Task<bool> Delete(string key)
        {
            _cache.Delete(key);
            if (_rangeCache != null) InvalidateRange(key);
            return await _persistStorage.Delete(key).ConfigureAwait(false);
        }

        private void InvalidateRange(string key)
        {
            _rangeCache.Delete("");

            for (var i = 1; i < key.Length; i++)
            {
                _rangeCache.Delete(key.Substring(0, i));
            }
        }
    }
}
