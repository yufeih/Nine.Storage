namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class RecycledStorage<T> : IStorage<T>
    {
        private readonly IStorage<T> _storage;
        private readonly IStorage<T> _recycleBin;

        public RecycledStorage(IStorage<T> storage, IStorage<T> recycleBin)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            if (recycleBin == null) throw new ArgumentNullException(nameof(recycleBin));

            _storage = storage;
            _recycleBin = recycleBin;
        }

        public async Task<bool> Delete(string key)
        {
            var existing = await _storage.Get(key).ConfigureAwait(false);
            if (existing != null) await _recycleBin.Put(key, existing);
            return await _storage.Delete(key).ConfigureAwait(false);
        }

        public Task<bool> Add(string key, T value) => _storage.Add(key, value);
        public Task<T> Get(string key) => _storage.Get(key);
        public Task Put(string key, T value) => _storage.Put(key, value);
        public Task<IEnumerable<T>> Range(string minKey, string maxKey, int? count = default(int?)) => _storage.Range(minKey, maxKey, count);
    }
}
