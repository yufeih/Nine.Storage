namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class PersistedStorageTest : StorageSpec<PersistedStorageTest>
    {
        public override IEnumerable<Func<IStorage<TestStorageObject>>> GetData()
        {
            return new[]
            {
                new Func<IStorage<TestStorageObject>>(() => PersistedStorage<TestStorageObject>.GetOrCreateAsync(Guid.NewGuid().ToString()).Result),
                new Func<IStorage<TestStorageObject>>(() => new PersistedStorageForTest<TestStorageObject>(Guid.NewGuid().ToString())),
                new Func<IStorage<TestStorageObject>>(() => new PersistedStorageForTest2<TestStorageObject>(Guid.NewGuid().ToString())),
            };
        }
    }

    public class PersistedStorageForTest<T> : IStorage<T> where T : class, IKeyed, new()
    {
        private string name;

        public PersistedStorageForTest(string name)
        {
            this.name = name;
        }

        public async Task<T> Get(string key)
        {
            var storage = await PersistedStorage<T>.GetOrCreateAsync(name);
            return await storage.Get(key).ConfigureAwait(false);
        }

        public async Task<IEnumerable<T>> Range(string minKey, string maxKey, int? count = null)
        {
            var storage = await PersistedStorage<T>.GetOrCreateAsync(name);
            return await storage.Range(minKey, maxKey, count).ConfigureAwait(false);
        }

        public async Task<bool> Add(T value)
        {
            var storage = await PersistedStorage<T>.GetOrCreateAsync(name);
            return await storage.Add(value).ConfigureAwait(false);
        }

        public async Task Put(T value)
        {
            var storage = await PersistedStorage<T>.GetOrCreateAsync(name);
            await storage.Put(value).ConfigureAwait(false);
        }

        public async Task<bool> Delete(string key)
        {
            var storage = await PersistedStorage<T>.GetOrCreateAsync(name);
            return await storage.Delete(key).ConfigureAwait(false);
        }
    }

    public class PersistedStorageForTest2<T> : IStorage<T> where T : class, IKeyed, new()
    {
        private string name;
        private PersistedStorage<T> current;

        public PersistedStorageForTest2(string name)
        {
            this.name = name;
        }

        public async Task<T> Get(string key)
        {
            var storage = (current = await PersistedStorage<T>.GetOrCreateAsync(name));
            return await storage.Get(key).ConfigureAwait(false);
        }

        public async Task<IEnumerable<T>> Range(string minKey, string maxKey, int? count = null)
        {
            var storage = (current = await PersistedStorage<T>.GetOrCreateAsync(name));
            return await storage.Range(minKey, maxKey, count).ConfigureAwait(false);
        }

        public async Task<bool> Add(T value)
        {
            var storage = current ?? (current = await PersistedStorage<T>.GetOrCreateAsync(name));
            return await storage.Add(value).ConfigureAwait(false);
        }

        public async Task Put(T value)
        {
            var storage = current ?? (current = await PersistedStorage<T>.GetOrCreateAsync(name));
            await storage.Put(value).ConfigureAwait(false);
        }

        public async Task<bool> Delete(string key)
        {
            var storage = current ?? (current = await PersistedStorage<T>.GetOrCreateAsync(name));
            return await storage.Delete(key).ConfigureAwait(false);
        }
    }
}
