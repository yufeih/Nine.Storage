namespace Nine.Storage
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;
    using Nine.Formatting;
    using Nine.Storage.Batching;

    /// <summary>
    /// Represents a deferred storage where write operations are buffered and inserted into azure table in batches.
    /// Write operations are flushed every minute or when the 100 batch limit is reach for that partition.
    /// </summary>
    public class AzureTableBatchStorage<T> : IDisposable, IStorage<T>, IPartitionedDataSource<T> where T : class, IKeyed, new()
    {
        private const int MaxRecords = 100;

        /// <summary>
        /// The partition count determines the number of partitions to be used for the storage.
        /// Operations across the partitions cannot be batched. And a batched operation can contain
        /// a maximum of 100 operations within a single batch.
        /// </summary>
        private readonly int _partitionCount;
        private readonly int _partitionKeyLength;
        private readonly Lazy<Task<CloudTable>> _table;
        private readonly Batch[] _batches;

        /// <summary>
        /// Resolves an entity into a KeyedTableEntity.
        /// </summary>
        private readonly EntityResolver<KeyedTableEntity<T>> _entityResolver;
        private readonly KeyedTableEntityFormatter<T> _formatter;

        /// <summary>
        /// Initializes a new instance of BatchedTableStorage.
        /// </summary>
        public AzureTableBatchStorage(string connectionString, string tableName = null, int partitionCount = 0, int partitionKeyLength = 0, TextConverter textConverter = null)
            : this(CloudStorageAccount.Parse(connectionString), tableName, partitionCount, partitionKeyLength, textConverter)
        { }

        /// <summary>
        /// Initializes a new instance of BatchedTableStorage.
        /// </summary>
        public AzureTableBatchStorage(CloudStorageAccount storageAccount, string tableName = null, int partitionCount = 0, int partitionKeyLength = 0, TextConverter textConverter = null)
        {
            // Default to 1 partition
            if (partitionCount <= 0) partitionCount = 1;
            if (partitionCount < 1 || partitionCount > 1024) throw new ArgumentOutOfRangeException("partitionCount");
            if (storageAccount == null) throw new ArgumentNullException("storageAccount");

            _partitionCount = partitionCount;
            _partitionKeyLength = partitionKeyLength;
            _batches = Enumerable.Range(0, partitionCount).Select(i => new Batch { PartitionKey = i.ToString(), Owner = this }).ToArray();
            _formatter = new KeyedTableEntityFormatter<T>(textConverter);

            _entityResolver = (string partitionKey, string rowKey, DateTimeOffset timestamp, IDictionary<string, EntityProperty> properties, string etag) =>
            {
                var entity = new KeyedTableEntity<T>(_formatter);
                entity.Data = new T();
                entity.ETag = etag;
                entity.Timestamp = timestamp;
                entity.ReadEntity(properties, null);
                return entity;
            };

            _table = new Lazy<Task<CloudTable>>(async () =>
            {
                var table = storageAccount.CreateCloudTableClient().GetTableReference(tableName ?? typeof(T).Name);
                await table.CreateIfNotExistsAsync().ConfigureAwait(false);
                return table;
            });
        }

        /// <summary>
        /// Calculates the partition key for a given key.
        /// </summary>
        public int GetPartitionKey(string key) => GetPartitionKey(key, _partitionCount, _partitionKeyLength);

        /// <summary>
        /// Calculates the partition key for a given key.
        /// </summary>
        public static int GetPartitionKey(string key, int partitionCount, int partitionKeyLength)
        {
            if (string.IsNullOrEmpty(key)) throw new NotSupportedException("key is empty");

            if (partitionKeyLength > 0)
            {
                key = key.Substring(0, Math.Min(partitionKeyLength, key.Length));
            }
            return Math.Abs(GetHashCode(key) % partitionCount);
        }

        /// <summary>
        /// The String.GetHashCode implementation is different between different platforms, 
        /// so need to have our own implementation to make it consistent.
        /// </summary>
        private static int GetHashCode(string value)
        {
            // Modified from https://gist.github.com/gerriten/7542231#file-gethashcode32-net

            var lastCharInd = value.Length - 1;
            var num1 = 0x15051505;
            var num2 = num1;
            var ind = 0;
            while (ind <= lastCharInd)
            {
                var ch = value[ind];
                var nextCh = ++ind > lastCharInd ? '\0' : value[ind];
                num1 = (((num1 << 5) + num1) + (num1 >> 0x1b)) ^ (nextCh << 16 | ch);
                if (++ind > lastCharInd) break;
                ch = value[ind];
                nextCh = ++ind > lastCharInd ? '\0' : value[ind++];
                num2 = (((num2 << 5) + num2) + (num2 >> 0x1b)) ^ (nextCh << 16 | ch);
            }
            return num1 + num2 * 0x5d588b65;
        }

        /// <summary>
        /// Gets an unique key value pair based on the specified key. Returns null if the key is not found.
        /// </summary>
        public async Task<T> Get(string key)
        {
            T value;
            var partitionKey = GetPartitionKey(key);

            // Lookup memory first in case the value has not been flushed to the storage.
            if (_batches[partitionKey].Items.TryGetValue(key, out value)) return value;

            // Lookup the storage for persisted records.
            var result = await (await _table.Value.ConfigureAwait(false)).ExecuteAsync(TableOperation.Retrieve(partitionKey.ToString(), key, _entityResolver)).ConfigureAwait(false);
            if (result == null || result.Result == null) return null;
            return (((KeyedTableEntity<T>)result.Result).Data);
        }

        /// <summary>
        /// Gets a list of key value pairs whose keys are inside the specified range.
        /// </summary>
        public async Task<IEnumerable<T>> Range(string minKey, string maxKey, int? count)
        {
            int? partitionKey = null;

            if (!(minKey == null && maxKey == null))
            {
                var minPartitionKey = GetPartitionKey(minKey);
                var maxPartitionKey = GetPartitionKey(maxKey);

                if (minPartitionKey != maxPartitionKey) throw new NotSupportedException("minKey and maxKey has to be in the same partition");

                partitionKey = minPartitionKey;
            }

            if (count != null && partitionKey == null) throw new NotSupportedException("Does not support count when minKey or maxKey is not specified");

            var result = new List<T>();

            // Lookup memory first in case the value has not been flushed to the storage.
            result.AddRange(
                from pair in (partitionKey != null ? _batches[partitionKey.Value].Items : _batches.SelectMany(x => x.Items))
                let key = pair.Key
                where (minKey == null || string.CompareOrdinal(key, minKey) >= 0) &&
                      (maxKey == null || string.CompareOrdinal(key, maxKey) < 0)
                select (T)pair.Value);

            // Lookup the storage for persisted records.
            var query = new TableQuery { TakeCount = count };
            if (partitionKey != null)
            {
                query.FilterString = TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition("PartitionKey", "eq", partitionKey.Value.ToString()), TableOperators.And,
                    TableQuery.CombineFilters(TableQuery.GenerateFilterCondition("RowKey", "ge", minKey), TableOperators.And,
                                              TableQuery.GenerateFilterCondition("RowKey", "lt", maxKey)));
            }

            TableContinuationToken continuation = null;
            while (true)
            {
                var queryResult = await (await _table.Value.ConfigureAwait(false)).ExecuteQuerySegmentedAsync(query, _entityResolver, continuation).ConfigureAwait(false);
                continuation = queryResult.ContinuationToken;
                result.AddRange(from item in queryResult.Results select (T)item.Data);
                if (continuation == null) break;
            }

            result.Sort(StorageObjectComparer.Comparer);
            return count != null ? result.Take(count.Value) : result;
        }

        /// <summary>
        /// Adds a new key value to the storage if the key does not already exist.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public Task<bool> Add(string key, T value)
        {
            // Azure table entity group transactions does not support attaching
            // many operations to a single entity, so we choose not to support 
            // tryadd (Insert) in this case.
            // This can be implemented by adding a new batches array if needed in the future.
            //
            // See http://msdn.microsoft.com/en-us/library/windowsazure/dd894038.aspx
            throw new NotSupportedException("BatchedTableStorage does not support Add");
        }

        /// <summary>
        /// Adds a key value pair to the storage if the key does not already exist,
        /// or updates a key value pair in the storage if the key already exists.
        /// </summary>
        public async Task Put(string key, T value)
        {
            var partitionKey = GetPartitionKey(key);
            await _batches[partitionKey].AddAsync(key, value, await _table.Value.ConfigureAwait(false)).ConfigureAwait(false);
        }

        /// <summary>
        /// Permanently removes the value with the specified key.
        /// </summary>
        public async Task<bool> Delete(string key)
        {
            T value;
            var partitionKey = GetPartitionKey(key);

            // If the value is not committed, don't need to remove from the storage.
            if (_batches[partitionKey].Items.TryRemove(key, out value)) return true;

            try
            {
                await (await _table.Value.ConfigureAwait(false)).ExecuteAsync(TableOperation.Delete(new TableEntity { PartitionKey = partitionKey.ToString(), RowKey = key, ETag = "*" })).ConfigureAwait(false);
                return true;
            }
            catch (StorageException e)
            {
                if (e.RequestInformation.HttpStatusCode != 404)
                {
                    throw;
                }
                return false;
            }
        }

        /// <summary>
        /// Flushes all the unsaved changes to the table storage
        /// </summary>
        public async Task Flush()
        {
            var tableValue = await _table.Value.ConfigureAwait(false);
            await Task.WhenAll(from x in _batches select x.FlushAsync(tableValue)).ConfigureAwait(false);
        }

        public Task<IEnumerable<string>> GetPartitions()
        {
            return Task.FromResult(Enumerable.Range(0, _partitionCount).Select(i => i.ToString()));
        }

        public IAsyncEnumerator<T> GetValues(string partition)
        {
            var continuation = (TableContinuationToken)null;
            var query = new TableQuery { FilterString = TableQuery.GenerateFilterCondition("PartitionKey", "eq", partition) };

            return AsyncEnumerator.Create(new Func<Task<AsyncEnumerationResult<T>>>(async () =>
            {
                var queryResult = await (await _table.Value.ConfigureAwait(false)).ExecuteQuerySegmentedAsync(query, _entityResolver, continuation).ConfigureAwait(false);
                continuation = queryResult.ContinuationToken;
                return new AsyncEnumerationResult<T>
                {
                    HasMore = continuation != null,
                    Items = queryResult.Results.Select(r => (T)r.Data),
                };
            }));
        }

        /// <summary>
        /// Flushes all the data in the memory to the storage.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual async void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    await Flush();
                }
                catch (Exception e)
                {
                    Trace.TraceError(e.ToString());
                }
            }
        }

        /// <summary>
        /// Represents a single table batch operation.
        /// </summary>
        class Batch
        {
            private int HasValue;
            public string PartitionKey;
            public AzureTableBatchStorage<T> Owner;
            public ConcurrentDictionary<string, T> Items = new ConcurrentDictionary<string, T>();

            /// <summary>
            /// Adds a new item to the batch, flush the table if necessary.
            /// </summary>
            public Task AddAsync(string key, T value, CloudTable table)
            {
                HasValue = 1;
                Items.AddOrUpdate(key, value, (a, b) => value);
                if (Items.Count >= MaxRecords)
                {
                    return FlushAsync(table);
                }
                return CommonTasks.Completed;
            }

            /// <summary>
            /// Flushes this batch.
            /// </summary>
            public Task FlushAsync(CloudTable table)
            {
                // Flush only when this batch is not empty
                if (Interlocked.CompareExchange(ref HasValue, 0, 1) == 1)
                {
                    var items = Interlocked.Exchange(ref Items, new ConcurrentDictionary<string, T>());
                    return Task.WhenAll(from x in GetBatchOperations(items) where x.Count > 0 select table.ExecuteBatchAsync(x));
                }
                return CommonTasks.Completed;
            }

            /// <summary>
            /// Turns the items into batches.
            /// </summary>
            private IEnumerable<TableBatchOperation> GetBatchOperations(ConcurrentDictionary<string, T> items)
            {
                var count = 0;
                var batch = new TableBatchOperation();
                foreach (var item in items)
                {
                    var entity = new KeyedTableEntity<T>(Owner._formatter) { Data = item.Value, PartitionKey = PartitionKey, RowKey = item.Key };
                    batch.Add(TableOperation.InsertOrReplace(entity));
                    if (++count >= MaxRecords)
                    {
                        yield return batch;
                        count = 0;
                        batch = new TableBatchOperation();
                    }
                }
                yield return batch;
            }
        }
    }

    class StorageObjectComparer : IComparer<IKeyed>
    {
        public static readonly IComparer<IKeyed> Comparer = new StorageObjectComparer();

        public int Compare(IKeyed x, IKeyed y)
        {
            return string.CompareOrdinal(x.GetKey(), y.GetKey());
        }
    }
}
