namespace Nine.Storage.Caching
{
    using System;
    using System.Collections.Concurrent;
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

        private readonly ConcurrentDictionary<string, Task<T>> _ongoingGets = new ConcurrentDictionary<string, Task<T>>();
        private readonly Lazy<ConcurrentDictionary<string, Task<IEnumerable<T>>>> _ongoingRanges =
                     new Lazy<ConcurrentDictionary<string, Task<IEnumerable<T>>>>(() => new ConcurrentDictionary<string, Task<IEnumerable<T>>>());

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
        public Task<T> Get(string key)
        {
            IncrementTotalCount();

            T result;
            if (_cache.TryGet(key, out result)) return Task.FromResult(result);

            Interlocked.Increment(ref CachedStorageStatus._missedCount);
            Missed?.Invoke(key);

            return _ongoingGets.GetOrAdd(key, _ =>
            {
                var tcs = new TaskCompletionSource<T>();
                _persistStorage.Get(key).ContinueWith(task =>
                {
                    if (task.IsCanceled) { tcs.TrySetCanceled(); return; }
                    if (task.IsFaulted) { tcs.TrySetException(task.Exception); return; }

                    Task<T> value;
                    var persisted = task.Result;
                    _cache.Put(key, persisted);
                    _ongoingGets.TryRemove(key, out value);
                    tcs.TrySetResult(persisted);
                });
                return tcs.Task;
            });
        }

        /// <summary>
        /// Gets a list of key value pairs whose keys are inside the specified range.
        /// </summary>
        public Task<IEnumerable<T>> Range(string minKey, string maxKey, int? maxCount)
        {
            IncrementTotalCount();

            CachedStorageItems<T> items;
            var canCache = (_rangeCache != null && maxCount == null && StorageKey.IsIncrement(minKey, maxKey));
            var cacheKey = canCache ? (minKey ?? "") : null;
            if (cacheKey != null && _rangeCache.TryGet(cacheKey, out items) && items != null)
            {
                return Task.FromResult<IEnumerable<T>>(items.Items);
            }

            Interlocked.Increment(ref CachedStorageStatus._missedCount);
            Missed?.Invoke(cacheKey);

            if (cacheKey == null) return _persistStorage.Range(minKey, maxKey, maxCount);

            return _ongoingRanges.Value.GetOrAdd(cacheKey, _ =>
            {
                var tcs = new TaskCompletionSource<IEnumerable<T>>();
                _persistStorage.Range(minKey, maxKey, null).ContinueWith(task =>
                {
                    if (task.IsCanceled) { tcs.TrySetCanceled(); return; }
                    if (task.IsFaulted) { tcs.TrySetException(task.Exception); return; }

                    Task<IEnumerable<T>> value;
                    var persisted = task.Result.ToArray();
                    _rangeCache.Put(cacheKey, new CachedStorageItems<T> { Items = persisted, Key = minKey });
                    _ongoingRanges.Value.TryRemove(cacheKey, out value);
                    tcs.TrySetResult(persisted);
                });
                return tcs.Task;
            });
        }

        private void IncrementTotalCount()
        {
            if (Interlocked.Increment(ref CachedStorageStatus._totalCount) > 100000)
            {
                Interlocked.Exchange(ref CachedStorageStatus._totalCount, 1);
                Interlocked.Exchange(ref CachedStorageStatus._missedCount, 0);
            }
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
        public Task<bool> Delete(string key)
        {
            _cache.Delete(key);
            if (_rangeCache != null) InvalidateRange(key);
            return _persistStorage.Delete(key);
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
