namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using ProtoBuf;

    /// <summary>
    /// Contains status about memory cached storage.
    /// </summary>
    public static class CachedStorageStatus
    {
        internal static long missedCount = 0;
        internal static long totalCount = 0;

        public static double HitRate
        {
            get { return totalCount > 0 ? 1.0 * (totalCount - missedCount) / totalCount : 0.0; }
        }
    }

    [ProtoContract]
    public class CachedStorageItems<T> : IKeyed
    {
        [ProtoMember(1)]
        public T[] Items { get; set; }
        [ProtoMember(2)]
        public string Key { get; set; }
        [ProtoMember(3)]
        public DateTime Time { get; set; }

        public string GetKey() { return Key; }
    }

    /// <summary>
    /// Represents a storage where objects are cached in the cache storage.
    /// </summary>
    public class CachedStorage<T> : IStorage<T> where T : class, IKeyed, new()
    {
        /// <summary>
        /// Since MemoryCache does not support null value, this constant identifies values that does
        /// not exist in the persisted storage.
        /// </summary>
        private static readonly object emptyObject = new object();

        private readonly IStorage<T> persistStorage;
        private readonly ICache<T> cache;
        private readonly ICache<CachedStorageItems<T>> rangeCache;

        public event Action<string> Missed;

        /// <summary>
        /// Gets or sets a value indicating whether put should modify an existing instance.
        /// This ensures that objects with the same key always shares the same object reference.
        /// </summary>
        public bool ReuseExistingInstance { get; set; }

        public CachedStorage(IStorage<T> persistedStorage, ICache<T> cache, ICache<CachedStorageItems<T>> rangeCache = null)
        {
            if (persistedStorage == null) throw new ArgumentNullException(nameof(persistedStorage));
            if (cache == null) throw new ArgumentNullException(nameof(cache));

            this.persistStorage = persistedStorage;
            this.cache = cache;
            this.rangeCache = rangeCache;
        }

        /// <summary>
        /// Gets an unique key value pair based on the specified key. Returns null if the key is not found.
        /// </summary>
        public async Task<T> Get(string key)
        {
            IncrementTotalCount();

            T result;
            if (cache.TryGet(key, out result)) return result;

            IncrementMissedCount(key);

            var persisted = await persistStorage.Get(key).ConfigureAwait(false);
            cache.Put(key, persisted);
            return persisted;
        }

        /// <summary>
        /// Gets a list of key value pairs whose keys are inside the specified range.
        /// </summary>
        public async Task<IEnumerable<T>> Range(string minKey, string maxKey, int? maxCount)
        {
            IncrementTotalCount();

            CachedStorageItems<T> items;
            var canCache = (rangeCache != null && maxCount == null && StorageKey.IsIncrement(minKey, maxKey));
            var cacheKey = canCache ? (minKey ?? "") : null;
            if (cacheKey != null && rangeCache.TryGet(cacheKey, out items) && items != null)
            {
                return items.Items;
            }

            IncrementMissedCount(cacheKey);

            var persisted = (await persistStorage.Range(minKey, maxKey, maxCount).ConfigureAwait(false)).ToArray();
            if (cacheKey != null)
            {
                rangeCache.Put(cacheKey, new CachedStorageItems<T> { Items = persisted, Key = minKey });
            }
            return persisted;
        }

        private void IncrementTotalCount()
        {
            if (Interlocked.Increment(ref CachedStorageStatus.totalCount) > 100000)
            {
                Interlocked.Exchange(ref CachedStorageStatus.totalCount, 1);
                Interlocked.Exchange(ref CachedStorageStatus.missedCount, 0);
            }
        }

        private void IncrementMissedCount(string key)
        {
            Interlocked.Increment(ref CachedStorageStatus.missedCount);

            var missed = Missed;
            if (missed != null) missed(key);
        }

        /// <summary>
        /// Adds a new key value to the storage if the key does not already exist.
        /// </summary>
        public async Task<bool> Add(T value)
        {
            if (await persistStorage.Add(value).ConfigureAwait(false))
            {
                var key = value.GetKey();

                // SHOULD override existing cache using Put !!!
                cache.Put(key, value);

                if (rangeCache != null) InvalidateRange(key);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Adds a key value pair to the storage if the key does not already exist,
        /// or updates a key value pair in the storage if the key already exists.
        /// </summary>
        public async Task Put(T value)
        {
            var key = value.GetKey();

            if (ReuseExistingInstance && value != null)
            {
                var target = await Get(value.GetKey());
                if (target != null)
                {
                    Merge(target, value);
                    value = target;
                }
            }

            await persistStorage.Put(value).ConfigureAwait(false);

            cache.Put(key, value);
            if (rangeCache != null) InvalidateRange(key);
        }

        /// <summary>
        /// Permanently removes the value with the specified key.
        /// </summary>
        public async Task<bool> Delete(string key)
        {
            cache.Delete(key);
            if (rangeCache != null) InvalidateRange(key);
            return await persistStorage.Delete(key).ConfigureAwait(false);
        }

        private void InvalidateRange(string key)
        {
            rangeCache.Delete("");

            for (var i = 1; i < key.Length; i++)
            {
                rangeCache.Delete(key.Substring(0, i));
            }
        }

        private static void Merge(T target, T change)
        {
            lock (target)
            {
                foreach (var pi in mergeProperties)
                {
                    pi.SetMethod.Invoke(target, new[] { pi.GetMethod.Invoke(change, null) });
                }

                foreach (var pi in mergeFields)
                {
                    pi.SetValue(target, pi.GetValue(change));
                }
            }
        }

        private static readonly PropertyInfo[] mergeProperties = (
            from pi in typeof(T).GetTypeInfo().DeclaredProperties
            where pi.GetMethod != null && pi.GetMethod.IsPublic && pi.SetMethod != null && pi.SetMethod.IsPublic
            select pi).ToArray();

        private static readonly FieldInfo[] mergeFields = (
            from fi in typeof(T).GetTypeInfo().DeclaredFields where fi.IsPublic select fi).ToArray();
    }
}
