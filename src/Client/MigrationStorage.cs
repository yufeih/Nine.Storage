namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class MigrationStorage<T> : IStorage<T> where T : class, IStorageObject, new()
    {
        private readonly IStorage<T> newStorage;
        private readonly IStorage<T>[] oldStorage;

        public MigrationStorage(IStorage<T> newStorage, params IStorage<T>[] oldStorage)
        {
            if (newStorage == null) throw new ArgumentNullException("newStorage");

            this.newStorage = newStorage;
            this.oldStorage = oldStorage;
        }

        public async Task<T> GetAsync(string key)
        {
            var value = await newStorage.GetAsync(key);

            if (value == null && oldStorage != null)
            {
                return (await Task.WhenAll(from x in oldStorage select x.GetAsync(key))).FirstOrDefault();
            }

            return value;
        }

        public async Task<IEnumerable<T>> GetManyAsync(string minKey = null, string maxKey = null, int? count = null)
        {
            var hasSecondaryValues = false;
            var result = (await newStorage.GetManyAsync(minKey, maxKey, count)).ToDictionary(x => x.GetKey(), x => x);
            if (oldStorage != null)
            {
                var oldValues = await Task.WhenAll(from x in oldStorage select x.GetManyAsync(minKey, maxKey, count));
                foreach (var value in oldValues.SelectMany(x => x))
                {
                    var key = value.GetKey();
                    if (!result.ContainsKey(key))
                    {
                        hasSecondaryValues = true;
                        result.Add(key, value);
                    }
                }
            }

            if (hasSecondaryValues && count != null)
            {
                return result.Values.OrderBy(x => x.GetKey()).Take(count.Value);
            }

            return result.Values;
        }

        public Task<bool> AddAsync(string key, T value)
        {
            return newStorage.AddAsync(key, value);
        }

        public Task PutAsync(string key, T value)
        {
            return newStorage.PutAsync(key, value);
        }

        public Task<bool> RemoveAsync(string key)
        {
            return newStorage.RemoveAsync(key);
        }
    }
}
