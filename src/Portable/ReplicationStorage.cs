namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class ReplicationStorage<T> : IStorage<T> where T : class, IStorageObject, new()
    {
        private readonly IStorage<T> primary;
        private readonly IStorage<T>[] replicas;

        public ReplicationStorage(IStorage<T> primary, params IStorage<T>[] replicas)
        {
            this.primary = primary;
            this.replicas = replicas;
        }

        public Task<T> GetAsync(string key)
        {
            return primary.GetAsync(key);
        }

        public Task<IEnumerable<T>> GetRangeAsync(string minKey, string maxKey, int? count = default(int?))
        {
            return primary.GetRangeAsync(minKey, maxKey, count);
        }

        public Task<bool> AddAsync(T value)
        {
            return primary.AddAsync(value);
        }

        public Task PutAsync(T value)
        {
            return primary.PutAsync(value);
        }

        public Task<bool> RemoveAsync(string key)
        {
            return primary.RemoveAsync(key);
        }
    }
}
