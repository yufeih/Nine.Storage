namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class ObservableStorage<T> : SyncSource<T>, IStorage<T> where T : class, IKeyed, new()
    {
        private readonly IStorage<T> storage;

        public ObservableStorage(IStorage<T> storage)
        {
            if (storage == null) throw new ArgumentNullException("storage");

            this.storage = storage;
        }

        public Task<T> Get(string key)
        {
            return storage.Get(key);
        }

        public Task<IEnumerable<T>> Range(string minKey, string maxKey, int? count = null)
        {
            return storage.Range(minKey, maxKey, count);
        }

        public async Task<bool> Add(T value)
        {
            if (!await storage.Add(value).ConfigureAwait(false)) return false;
            Notify(new Delta<T>(DeltaAction.Add, value.GetKey(), value));
            return true;
        }

        public async Task Put(T value)
        {
            await storage.Put(value).ConfigureAwait(false);
            Notify(new Delta<T>(DeltaAction.Put, value.GetKey(), value));
        }

        public async Task<bool> Delete(string key)
        {
            if (!await storage.Delete(key).ConfigureAwait(false)) return false;
            Notify(new Delta<T>(DeltaAction.Remove, key));
            return true;
        }
    }
}
