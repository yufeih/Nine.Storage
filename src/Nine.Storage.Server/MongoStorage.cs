namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using MongoDB.Bson;
    using MongoDB.Bson.Serialization;
    using MongoDB.Bson.Serialization.Serializers;
    using MongoDB.Driver;
    using MongoDB.Driver.Builders;

    public class MongoStorage<T> : IStorage<T>
    {
        class StorageObject
        {
            public string Id { get; set; }
            public T Value { get; set; }
        }

        private readonly MongoClient client;
        private readonly MongoDatabase database;
        private readonly MongoCollection<StorageObject> collection;

        static MongoStorage()
        {
            // http://alexmg.com/datetime-precision-with-mongodb-and-the-c-driver/
            // http://stackoverflow.com/questions/16185262/what-is-new-way-of-setting-datetimeserializationoptions-defaults-in-mongodb-c-sh
            BsonSerializer.RegisterSerializer(typeof(DateTime), new DateTimeSerializer(DateTimeKind.Utc, BsonType.Document));
        }

        public MongoStorage(string connection, string collectionName = null)
        {
            var url = new MongoUrl(connection);
            this.client = new MongoClient(url);
            this.database = client.GetServer().GetDatabase(url.DatabaseName);
            this.collection = this.database.GetCollection<StorageObject>(collectionName ?? typeof(T).Name);
        }

        public Task<T> Get(string key)
        {
            var result = collection.FindOneById(key);
            return Task.FromResult(result != null ? result.Value : default(T));
        }

        public Task<IEnumerable<T>> Range(string minKey = null, string maxKey = null, int? count = null)
        {
            var conditions = new List<IMongoQuery>();
            if (minKey != null) conditions.Add(Query.GTE("_id", minKey));
            if (maxKey != null) conditions.Add(Query.LT("_id", maxKey));

            var query = collection.Find(conditions.Count > 0 ? Query.And(conditions) : null);
            query.SetSortOrder(SortBy.Ascending("_id"));
            if (count != null)
            {
                query.SetLimit(count.Value);
            }

            return Task.FromResult(from x in query select x.Value);
        }

        public Task<bool> Add(string key, T value)
        {
            try
            {
                return Task.FromResult(collection.Insert(new StorageObject { Id = key, Value = value }).DocumentsAffected == 1);
            }
            catch (MongoDuplicateKeyException)
            {
                return Task.FromResult(false);
            }
        }

        public Task Put(string key, T value)
        {
            return Task.FromResult(collection.Save(new StorageObject { Id = key, Value = value }).DocumentsAffected == 1);
        }

        public Task<bool> Delete(string key)
        {
            return Task.FromResult(collection.Remove(Query.EQ("_id", key)).DocumentsAffected == 1);
        }
    }
}
