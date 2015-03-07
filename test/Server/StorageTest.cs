namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using Xunit;

    class AzureStorageTest : StorageSpec<AzureStorageTest>
    {
        private const string AzureStorageConnection = "";

        public override IEnumerable<ITestFactory<IStorage<TestStorageObject>>> GetData()
        {
            return new[]
            {
                new TestFactory<IStorage<TestStorageObject>>(typeof(BatchedTableStorage<>), () => new BatchedTableStorage<TestStorageObject>(AzureStorageConnection, "BatchedTableStorage" + Environment.TickCount.ToString())),
                new TestFactory<IStorage<TestStorageObject>>(typeof(TableStorage<>), () => new TableStorage<TestStorageObject>(AzureStorageConnection, "TableStorage" + Environment.TickCount.ToString()))
            };
        }
    }

    class ServerStorageTest : StorageSpec<ServerStorageTest>
    {
        private const string ElasticSearchEndpoint = "";
        private const string MongoDbEndpoint = "";
        private const string MemcacheEndpoint = "";
        private const string RedisEndpoint = "";

        public override IEnumerable<ITestFactory<IStorage<TestStorageObject>>> GetData()
        {
            return new[]
            {
                new TestFactory<IStorage<TestStorageObject>>(typeof(ElasticSearchStorage<>), () => new ElasticSearchStorage<TestStorageObject>(ElasticSearchEndpoint, "ElasticSearchTest-" + Environment.TickCount.ToString())),
                new TestFactory<IStorage<TestStorageObject>>(typeof(MongoStorage<>), () => new MongoStorage<TestStorageObject>(MongoDbEndpoint, Environment.TickCount.ToString())),
                new TestFactory<IStorage<TestStorageObject>>(typeof(MemcachedStorage<>), () => new MemcachedStorage<TestStorageObject>(MemcacheEndpoint, Environment.TickCount.ToString())),
                new TestFactory<IStorage<TestStorageObject>>(typeof(RedisStorage<>), () => new RedisStorage<TestStorageObject>(RedisEndpoint,  Environment.TickCount.ToString())),
            };
        }
    }
}
