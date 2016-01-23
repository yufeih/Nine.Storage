namespace Nine.Storage
{
    using System;
    using System.ComponentModel;
    using System.Threading.Tasks;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class StorageExtensions
    {
        public static Task Delete<T>(this IStorage<T> storage, T value) where T : class, IKeyed, new()
            => storage.Delete(value.GetKey());

        public static Task Delete<T>(this IStorage storage, T value) where T : class, IKeyed, new()
            => storage.Delete<T>(value.GetKey());

        public static Task<bool> Patch<T>(this IStorage storage, string key, Action<T> action) where T : class, IKeyed, new()
            => Patch(storage, key, x => true, action);

        public static async Task<bool> Patch<T>(this IStorage storage, string key, Func<T, bool> predicate, Action<T> action) where T : class, IKeyed, new()
        {
            var existing = await storage.Get<T>(key).ConfigureAwait(false);
            if (existing == null) return false;
            if (predicate != null && !predicate(existing)) return false;

            var cloned = ObjectHelper<T>.Clone(existing);
            action(cloned);
            await storage.Put(cloned).ConfigureAwait(false);
            return true;
        }
    }
}