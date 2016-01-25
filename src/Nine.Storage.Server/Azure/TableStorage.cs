namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;
    using Nine.Formatting;

    /// <summary>
    /// Represents a straightforward keyed storage system for windows azure. 
    /// All the read operations are read directly from the table storage.
    /// All the write operations are immediately written to the table storage.
    /// </summary>
    /// <remarks>
    /// The keys are directly mapped to PartitionKey of azure table entity.
    /// This table storage can get as much as around 500 records per second on a small 
    /// azure instance. This performance figure is measured on Jan 26, 2014.
    /// </remarks>
    public class TableStorage<T> : IStorage<T> where T : class, new()
    {
        private readonly bool _treatKeyAsPartitionKey;
        private readonly LazyAsync<CloudTable> _table;

        /// <summary>
        /// Resolves an entity into a KeyedTableEntity.
        /// </summary>
        private readonly EntityResolver<KeyedTableEntity<T>> _entityResolver;
        private readonly KeyedTableEntityFormatter<T> _formatter;

        /// <summary>
        /// Initializes a new instance of TableStorage.
        /// </summary>
        public TableStorage(string connectionString, string tableName = null, bool treatKeyAsPartitionKey = false, TextConverter textConverter = null)
            : this(CloudStorageAccount.Parse(connectionString), tableName, treatKeyAsPartitionKey, textConverter)
        { }

        /// <summary>
        /// Initializes a new instance of TableStorage.
        /// </summary>
        public TableStorage(CloudStorageAccount storageAccount, string tableName = null, bool treatKeyAsPartitionKey = false, TextConverter textConverter = null)
        {
            if (storageAccount == null) throw new ArgumentNullException("storageAccount");

            _formatter = new KeyedTableEntityFormatter<T>(textConverter);
            _entityResolver = ResolveEntity;
            _treatKeyAsPartitionKey = treatKeyAsPartitionKey;
            _table = new LazyAsync<CloudTable>(async () =>
            {
                // NOTE: 
                // Azure may blame 409 (Conflict) if you are trying to create the table and immediately after it is deleted
                // For now we choose not to delete any of the tables in production servers.
                // http://stackoverflow.com/questions/15508517/the-correct-way-to-delete-and-recreate-a-windows-azure-storage-table-error-409
                var table = storageAccount.CreateCloudTableClient().GetTableReference(tableName ?? typeof(T).Name);
                await table.CreateIfNotExistsAsync().ConfigureAwait(false);
                return table;
            });
        }

        /// <summary>
        /// Gets an unique key value pair based on the specified key. Returns null if the key is not found.
        /// </summary>
        public async Task<T> Get(string key)
        {
            var operation = _treatKeyAsPartitionKey ? TableOperation.Retrieve(key, "", _entityResolver) : TableOperation.Retrieve("", key, _entityResolver);
            var result = await (await _table.GetValueAsync().ConfigureAwait(false)).ExecuteAsync(operation).ConfigureAwait(false);
            if (result == null || result.Result == null) return null;
            return (((KeyedTableEntity<T>)result.Result).Data);
        }

        /// <summary>
        /// Gets a list of key value pairs whose keys are inside the specified range.
        /// </summary>
        public async Task<IEnumerable<T>> Range(string minKey, string maxKey, int? count)
        {
            if (count != null && count > 1000) throw new ArgumentOutOfRangeException("count cannot be larger than 1000");
            if (count != null && count <= 0) throw new ArgumentOutOfRangeException("count cannot be less than or equal to 0");

            var result = new List<T>();
            var query = new TableQuery().Take(count);
            var row = _treatKeyAsPartitionKey ? "RowKey" : "PartitionKey";
            var partition = _treatKeyAsPartitionKey ? "PartitionKey" : "RowKey";

            if (minKey != null)
            {
                query.FilterString = TableQuery.GenerateFilterCondition(partition, "ge", minKey);
            }

            if (maxKey != null)
            {
                var maxFilter = TableQuery.GenerateFilterCondition(partition, "lt", maxKey);
                if (query.FilterString != null)
                {
                    query.FilterString = TableQuery.CombineFilters(query.FilterString, TableOperators.And, maxFilter);
                }
                else
                {
                    query.FilterString = maxFilter;
                }
            }

            var partitionFilter = TableQuery.GenerateFilterCondition(row, "eq", "");

            if (string.IsNullOrEmpty(query.FilterString))
                query.FilterString = partitionFilter;
            else
                query.FilterString = TableQuery.CombineFilters(partitionFilter, TableOperators.And, query.FilterString);

            TableContinuationToken continuation = null;
            while (true)
            {
                var queryResult = await (await _table.GetValueAsync().ConfigureAwait(false)).ExecuteQuerySegmentedAsync(query, _entityResolver, continuation).ConfigureAwait(false);
                continuation = queryResult.ContinuationToken;
                result.AddRange(from item in queryResult.Results select (T)item.Data);
                if (count != null || continuation == null) break;
            }
            return result;
        }

        /// <summary>
        /// Adds a new key value to the storage if the key does not already exist.
        /// </summary>
        public async Task<bool> Add(string key, T value)
        {
            try
            {
                var entity = _treatKeyAsPartitionKey ?
                    new KeyedTableEntity<T>(_formatter) { Data = value, PartitionKey = key, RowKey = "" } :
                    new KeyedTableEntity<T>(_formatter) { Data = value, PartitionKey = "", RowKey = key };

                await (await _table.GetValueAsync().ConfigureAwait(false)).ExecuteAsync(TableOperation.Insert(entity)).ConfigureAwait(false);
                return true;
            }
            catch (StorageException ex)
            {
                // Conflict
                if (ex.RequestInformation.HttpStatusCode == 409) return false;
                throw;
            }
        }

        /// <summary>
        /// Adds a key value pair to the storage if the key does not already exist,
        /// or updates a key value pair in the storage if the key already exists.
        /// </summary>
        public async Task Put(string key, T value)
        {
            var entity = _treatKeyAsPartitionKey ?
                new KeyedTableEntity<T>(_formatter) { Data = value, PartitionKey = key, RowKey = "" } :
                new KeyedTableEntity<T>(_formatter) { Data = value, PartitionKey = "", RowKey = key };

            await (await _table.GetValueAsync().ConfigureAwait(false)).ExecuteAsync(TableOperation.InsertOrReplace(entity)).ConfigureAwait(false);
        }

        /// <summary>
        /// Permanently removes the value with the specified key.
        /// </summary>
        public async Task<bool> Delete(string key)
        {
            try
            {
                var entity = _treatKeyAsPartitionKey ?
                    new TableEntity { PartitionKey = key, RowKey = "", ETag = "*" } :
                    new TableEntity { PartitionKey = "", RowKey = key, ETag = "*" };

                await (await _table.GetValueAsync().ConfigureAwait(false)).ExecuteAsync(TableOperation.Delete(entity)).ConfigureAwait(false);
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

        private KeyedTableEntity<T> ResolveEntity(string partitionKey, string rowKey, DateTimeOffset timestamp, IDictionary<string, EntityProperty> properties, string etag)
        {
            var entity = new KeyedTableEntity<T>(_formatter);
            entity.Data = new T();
            entity.ETag = etag;
            entity.Timestamp = timestamp;
            entity.ReadEntity(properties, null);

            if (_treatKeyAsPartitionKey)
                entity.RowKey = "";
            else
                entity.PartitionKey = "";

            return entity;
        }
    }
}
