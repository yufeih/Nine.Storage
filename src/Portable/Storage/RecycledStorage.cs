﻿namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class RecycledStorage<T> : IStorage<T> where T : class, IKeyed, new()
    {
        private readonly IStorage<T> storage;
        private readonly IStorage<T> recycleBin;

        public RecycledStorage(IStorage<T> storage, IStorage<T> recycleBin)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            if (recycleBin == null) throw new ArgumentNullException(nameof(recycleBin));

            this.storage = storage;
            this.recycleBin = recycleBin;
        }

        public async Task<bool> Delete(string key)
        {
            var existing = await storage.Get(key).ConfigureAwait(false);
            if (existing != null) await recycleBin.Put(existing);
            return await storage.Delete(key).ConfigureAwait(false);
        }

        public Task<bool> Add(T value) => storage.Add(value);
        public Task<T> Get(string key) => storage.Get(key);
        public Task Put(T value) => storage.Put(value);
        public Task<IEnumerable<T>> Range(string minKey, string maxKey, int? count = default(int?)) => storage.Range(minKey, maxKey, count);
    }
}
