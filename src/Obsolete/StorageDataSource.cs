namespace Nine.Storage.Batching
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Nine.Storage.Compatibility;

    public class StorageDataSource<T> : IPartitionedDataSource<T> where T : IKeyed
    {
        private static readonly string[] ValidIdChars;

        private readonly IStorage<T> _storage;
        private readonly int _batchSize;
        private readonly string[] _validKeyCharactors;

        static StorageDataSource()
        {
            ValidIdChars =
                Enumerable.Range('a', 'z' - 'a').Concat(
                Enumerable.Range('A', 'Z' - 'A').Concat(
                Enumerable.Range('0', '9' - '0'))).Select(i => ((char)i).ToString()).ToArray();
        }

        public StorageDataSource(IStorage<T> storage, string[] validKeyCharactors = null, int batchSize = 1000)
        {
            if (storage == null) throw new ArgumentNullException("storage");

            _storage = storage;
            _batchSize = batchSize;
            _validKeyCharactors = validKeyCharactors;
        }

        public static IPartitionedDataSource<T> CreateIdStorage(IStorage<T> storage, int batchSize = 1000)
        {
            return new StorageDataSource<T>(storage, ValidIdChars, batchSize);
        }

        public Task<IEnumerable<string>> GetPartitions()
        {
            return Task.FromResult<IEnumerable<string>>(
                _validKeyCharactors != null ? _validKeyCharactors : new[] { "" });
        }

        public IAsyncEnumerator<T> GetValues(string partition)
        {
            if (partition == "") return _storage.All<T>(_batchSize);

            var continuation = partition;
            var max = StorageKey.Increment(partition);
            return AsyncEnumerator.Create(new Func<Task<AsyncEnumerationResult<T>>>(async () =>
            {
                var batch = await _storage.Range(continuation, max, _batchSize);
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
