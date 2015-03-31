namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading.Tasks;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class StorageExtensions
    {
        public static Task<T> Get<T>(this IStorage<T> storage, params object[] keyComponents) where T : class, IKeyed, new()
        {
            return storage.Get(StorageKey.Get(keyComponents));
        }

        public static Task<IEnumerable<T>> List<T>(this IStorage<T> storage, params object[] keyComponents) where T : class, IKeyed, new()
        {
            var prefix = StorageKey.Get(keyComponents);
            return storage.Range(prefix, StorageKey.Increment(prefix), null);
        }

        public static Task<IEnumerable<T>> Page<T>(this IStorage<T> storage, int? count, params object[] keyComponents) where T : class, IKeyed, new()
        {
            var prefix = StorageKey.Get(keyComponents);
            return storage.Range(prefix, StorageKey.Increment(prefix), count);
        }

        public static IAsyncEnumerator<T> All<T>(this IStorage<T> storage, int? batchSize = 1000) where T : class, IKeyed, new()
        {
            var continuation = (string)null;
            return AsyncEnumerator.Create(new Func<Task<AsyncEnumerationResult<T>>>(async () =>
            {
                var batch = await storage.Range(continuation, null, batchSize);
                var hasMore = batchSize != null && batch.Any();
                if (hasMore)
                {
                    continuation = StorageKey.Increment(batch.Last().GetKey());
                }
                return new AsyncEnumerationResult<T> { HasMore = hasMore, Items = batch };
            }));
        }

        public static Task Delete<T>(this IStorage<T> storage, T value) where T : class, IKeyed, new()
        {
            return storage.Delete(value.GetKey());
        }

        public static Task<T> Get<T>(this IStorage storage, params object[] keyComponents) where T : class, IKeyed, new()
        {
            return storage.Get<T>(StorageKey.Get(keyComponents));
        }

        public async static Task<T> GetOrAdd<T>(this IStorage storage, Func<T> factory = null, params object[] keyComponents) where T : class, IKeyed, new()
        {
            var result = await storage.Get<T>(StorageKey.Get(keyComponents)).ConfigureAwait(false);
            if (result == null)
            {
                result = factory != null ? factory() : new T();
                if (result != null) await storage.Put(result);
            }
            return result;
        }

        public static Task<IEnumerable<T>> List<T>(this IStorage storage, params object[] keyComponents) where T : class, IKeyed, new()
        {
            var prefix = StorageKey.Get(keyComponents);
            return storage.Range<T>(prefix, StorageKey.Increment(prefix), null);
        }

        public static Task<IEnumerable<T>> Page<T>(this IStorage storage, int? count, params object[] keyComponents) where T : class, IKeyed, new()
        {
            var prefix = StorageKey.Get(keyComponents);
            return storage.Range<T>(prefix, StorageKey.Increment(prefix), count);
        }

        public static IAsyncEnumerator<T> All<T>(this IStorage storage, int? batchSize = 1000) where T : class, IKeyed, new()
        {
            var continuation = (string)null;
            return AsyncEnumerator.Create(new Func<Task<AsyncEnumerationResult<T>>>(async () =>
            {
                var batch = await storage.Range<T>(continuation, null, batchSize);
                var hasMore = batchSize != null && batch.Any();
                if (hasMore)
                {
                    continuation = StorageKey.Increment(batch.Last().GetKey());
                }
                return new AsyncEnumerationResult<T> { HasMore = hasMore, Items = batch };
            }));
        }

        public static Task Delete<T>(this IStorage storage, T value) where T : class, IKeyed, new()
        {
            return storage.Delete<T>(value.GetKey());
        }

        public static async Task<bool> Put<T>(this IStorage storage, string key, Action<T> action, Func<T> factory = null) where T : class, IKeyed, new()
        {
            var existing = await storage.Get<T>(key).ConfigureAwait(false) ?? (factory != null ? factory() :  new T());
            action(existing);
            if (key != null && existing.GetKey() == null) throw new InvalidOperationException("Put with an invalid key");
            await storage.Put(existing).ConfigureAwait(false);
            return true;
        }

        public static async Task<bool> Put<T>(this IStorage storage, string key, Func<T, bool> predicate, Action<T> action, Func<T> factory = null) where T : class, IKeyed, new()
        {
            var existing = await storage.Get<T>(key).ConfigureAwait(false) ?? (factory != null ? factory() : new T());
            if (predicate(existing))
            {
                action(existing);
                if (key != null && existing.GetKey() == null) throw new InvalidOperationException("Put with an invalid key");
                await storage.Put(existing).ConfigureAwait(false);
                return true;
            }
            return false;
        }

        public static Task<bool> Patch<T>(this IStorage storage, string key, Action<T> action) where T : class, IKeyed, new()
        {
            return Patch(storage, key, x => true, action);
        }

        public static async Task<bool> Patch<T>(this IStorage storage, string key, Func<T, bool> predicate, Action<T> action) where T : class, IKeyed, new()
        {
            var existing = await storage.Get<T>(key).ConfigureAwait(false);
            if (existing == null) return false;
            if (predicate != null && !predicate(existing)) return false;

            action(existing);
            await storage.Put(existing).ConfigureAwait(false);
            return true;
        }
    }
}