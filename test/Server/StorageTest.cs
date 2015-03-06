namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;

    class AzureStorageTest : StorageSpec<AzureStorageTest>
    {
        private const string AzureStorageConnection = "";

        public override IEnumerable<Func<IStorage<TestStorageObject>>> GetData()
        {
            return new[]
            {
                new Func<IStorage<TestStorageObject>>(() => new BatchedTableStorage<TestStorageObject>(AzureStorageConnection, "BatchedTableStorage" + Environment.TickCount.ToString())),
                new Func<IStorage<TestStorageObject>>(() => new TableStorage<TestStorageObject>(AzureStorageConnection, "TableStorage" + Environment.TickCount.ToString()))
            };
        }
    }

    class ServerStorageTest : StorageSpec<ServerStorageTest>
    {
        private const string ElasticSearchEndpoint = "";
        private const string MongoDbEndpoint = "";
        private const string MemcacheEndpoint = "";
        private const string RedisEndpoint = "";

        public override IEnumerable<Func<IStorage<TestStorageObject>>> GetData()
        {
            return new[]
            {
                new Func<IStorage<TestStorageObject>>(() => new ElasticSearchStorage<TestStorageObject>(ElasticSearchEndpoint, "ElasticSearchTest-" + Environment.TickCount.ToString())),
                new Func<IStorage<TestStorageObject>>(() => new MongoStorage<TestStorageObject>(MongoDbEndpoint, Environment.TickCount.ToString())),
                new Func<IStorage<TestStorageObject>>(() => new MemcachedStorage<TestStorageObject>(MemcacheEndpoint, Environment.TickCount.ToString())),
                new Func<IStorage<TestStorageObject>>(() => new RedisStorage<TestStorageObject>(RedisEndpoint,  Environment.TickCount.ToString())),
            };
        }
    }
}
