namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using Nine.Formatting;
    using Xunit;

    public class ServerStorageTest : StorageSpec<ServerStorageTest>
    {
        public override IEnumerable<ITestFactory<IStorage<TestStorageObject>>> GetData()
        {/*
            if (!string.IsNullOrEmpty(Connection.Current.ElasticSearch))
                yield return new TestFactory<IStorage<TestStorageObject>>(
                    typeof(ElasticSearchStorage<>),
                    () => new ElasticSearchStorage<TestStorageObject>(
                        Connection.Current.ElasticSearch,
                        "ElasticSearchTest-" + Environment.TickCount.ToString()));
*/
            if (!string.IsNullOrEmpty(Connection.Current.Mongo))
                yield return new TestFactory<IStorage<TestStorageObject>>(
                    typeof(MongoStorage<>),
                    () => new MongoStorage<TestStorageObject>(
                        Connection.Current.Mongo,
                        Environment.TickCount.ToString()));

            if (!string.IsNullOrEmpty(Connection.Current.Memcached))
                yield return new TestFactory<IStorage<TestStorageObject>>(
                    typeof(MemcachedStorage<>),
                    () => new MemcachedStorage<TestStorageObject>(
                        Connection.Current.Memcached,
                        new JilFormatter(),
                        Environment.TickCount.ToString()));

            if (!string.IsNullOrEmpty(Connection.Current.Redis))
                yield return new TestFactory<IStorage<TestStorageObject>>(
                    typeof(RedisStorage<>),
                    () => new RedisStorage<TestStorageObject>(
                        Connection.Current.Redis,
                        new JilFormatter(),
                        Environment.TickCount.ToString()));

            yield return new TestFactory<IStorage<TestStorageObject>>("dummy", () => new MemoryStorage<TestStorageObject>());
        }
    }
}
