namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public interface IPartitionedDataSource<T>
    {
        Task<IEnumerable<string>> GetPartitions();

        IAsyncEnumerator<T> GetValues(string partition);
    }

    public class StorageDataSource<T> : IPartitionedDataSource<T> where T : class, IKeyed, new()
    {
        private static readonly string[] validIdChars;

        private readonly IStorage<T> storage;
        private readonly int batchSize;
        private readonly string[] validKeyCharactors;

        static StorageDataSource()
        {
            validIdChars =
                Enumerable.Range('a', 'z' - 'a').Concat(
                Enumerable.Range('A', 'Z' - 'A').Concat(
                Enumerable.Range('0', '9' - '0'))).Select(i => ((char)i).ToString()).ToArray();
        }

        public StorageDataSource(IStorage<T> storage, string[] validKeyCharactors = null, int batchSize = 1000)
        {
            if (storage == null) throw new ArgumentNullException("storage");

            this.storage = storage;
            this.batchSize = batchSize;
            this.validKeyCharactors = validKeyCharactors;
        }

        public static IPartitionedDataSource<T> CreateIdStorage(IStorage<T> storage, int batchSize = 1000)
        {
            return new StorageDataSource<T>(storage, validIdChars, batchSize);
        }

        public Task<IEnumerable<string>> GetPartitions()
        {
            return Task.FromResult<IEnumerable<string>>(
                validKeyCharactors != null ? validKeyCharactors : new[] { "" });
        }

        public IAsyncEnumerator<T> GetValues(string partition)
        {
            if (partition == "") return storage.All<T>(batchSize);

            var continuation = partition;
            var max = StorageKey.Increment(partition);
            return AsyncEnumerator.Create(new Func<Task<AsyncEnumerationResult<T>>>(async () =>
            {
                var batch = await storage.Range(continuation, max, batchSize);
                var hasMore = batch.Any();
                if (hasMore)
                {
                    continuation = StorageKey.Increment(batch.Last().GetKey());
                }
                return new AsyncEnumerationResult<T> { HasMore = hasMore, Items = batch };
            }));
        }
    }
}
