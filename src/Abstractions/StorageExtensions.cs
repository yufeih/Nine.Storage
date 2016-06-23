namespace Nine.Storage
{
    using System;
    using System.ComponentModel;
    using System.Reflection;
    using System.Threading.Tasks;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class StorageExtensions
    {
        public static Task<bool> Add<T>(this IStorage<T> storage, T value) where T : IKeyed
            => storage.Add(value.GetKey(), value);

        public static Task<bool> Add<T>(this IStorage storage, T value) where T : IKeyed
            => storage.Add(value.GetKey(), value);

        public static Task Put<T>(this IStorage<T> storage, T value) where T : IKeyed
            => storage.Put(value.GetKey(), value);

        public static Task Put<T>(this IStorage storage, T value) where T : IKeyed
            => storage.Put(value.GetKey(), value);

        public static Task Delete<T>(this IStorage<T> storage, T value) where T : IKeyed
            => storage.Delete(value.GetKey());

        public static Task Delete<T>(this IStorage storage, T value) where T : IKeyed
            => storage.Delete<T>(value.GetKey());

        public static Task<bool> Patch<T>(this IStorage storage, string key, Action<T> action) where T : class, new()
            => Patch(storage, key, x => true, action);

        public static async Task<bool> Patch<T>(this IStorage storage, string key, Func<T, bool> predicate, Action<T> action) where T : class, new()
        {
            var existing = await storage.Get<T>(key).ConfigureAwait(false);
            if (existing == null) return false;
            if (predicate != null && !predicate(existing)) return false;

            var cloned = existing.MemberwiseClone();
            action(cloned);
            await storage.Put(key, cloned).ConfigureAwait(false);
            return true;
        }
    }
}