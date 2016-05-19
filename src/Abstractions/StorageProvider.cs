namespace Nine.Storage
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;

    public abstract class StorageProvider
    {
        struct Entry { public Lazy<Task<object>> Initializer; public object Result; }

        private readonly ConcurrentDictionary<Type, Entry> _values = new ConcurrentDictionary<Type, Entry>();

        public IStorage<T> TryGetStorage<T>()
        {
            Entry entry;
            return _values.TryGetValue(typeof(T), out entry) ? (IStorage<T>)entry.Result : null;
        }

        public async Task<IStorage<T>> GetStorage<T>()
        {
            var factory = _values.GetOrAdd(typeof(T), type => new Entry
            {
                Initializer = new Lazy<Task<object>>(() => GetStorageCore<T>())
            });
            return (IStorage<T>)(await factory.Initializer.Value.ConfigureAwait(false));
        }

        private async Task<object> GetStorageCore<T>()
        {
            var storage = await CreateAsync<T>().ConfigureAwait(false);
            if (storage == null) throw new InvalidOperationException("Storage provider missing for " + typeof(T).Name);

            var entry = _values[typeof(T)];
            entry.Result = storage;
            _values[typeof(T)] = entry;
            return storage;
        }

        protected abstract Task<IStorage<T>> CreateAsync<T>();
    }
}
