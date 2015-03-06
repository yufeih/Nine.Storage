namespace Nine.Storage
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;

    public abstract class StorageProviderBase : IStorageProvider
    {
        private readonly ConcurrentDictionary<Type, LazyAsync<object>> values = new ConcurrentDictionary<Type, LazyAsync<object>>();

        public async Task<IStorage<T>> GetAsync<T>() where T : class, IKeyed, new()
        {
            var factory = values.GetOrAdd(typeof(T), type => new LazyAsync<object>(() => GetStorageCoreAsync<T>()));
            return (IStorage<T>)(await factory.GetValueAsync().ConfigureAwait(false));
        }

        private async Task<object> GetStorageCoreAsync<T>() where T : class, IKeyed, new()
        {
            var storage = await CreateAsync<T>().ConfigureAwait(false);
            if (storage == null) throw new InvalidOperationException("Storage provider missing for " + typeof(T).Name);
            return storage;
        }

        protected abstract Task<IStorage<T>> CreateAsync<T>() where T : class, IKeyed, new();
    }
}
