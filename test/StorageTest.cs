namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Nine.Formatting;
    using Nine.Storage.Caching;
    using SQLite.Net.Platform.Win32;
    using Xunit;

    public class StorageTest : StorageSpec<StorageTest>
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

            yield return new TestFactory<IStorage<TestStorageObject>>(typeof(SqliteStorage<>), () => new SqliteStorage<TestStorageObject>($"sqlite-{ Environment.TickCount }.db", new SQLitePlatformWin32(), new JsonFormatter()));

            yield return new TestFactory<IStorage<TestStorageObject>>(typeof(MemoryStorage<>), () => new MemoryStorage<TestStorageObject>());

            yield return new TestFactory<IStorage<TestStorageObject>>(typeof(MemoryStorage<>), () => new MemoryStorage<TestStorageObject>(true));

            yield return new TestFactory<IStorage<TestStorageObject>>(typeof(RecycledStorage<>), () => new RecycledStorage<TestStorageObject>(new MemoryStorage<TestStorageObject>(), new MemoryStorage<TestStorageObject>()));

            yield return new TestFactory<IStorage<TestStorageObject>>(typeof(CachedStorage<>), () => new CachedStorage<TestStorageObject>(new MemoryStorage<TestStorageObject>(), new MemoryStorage<TestStorageObject>()));

            yield return new TestFactory<IStorage<TestStorageObject>>(typeof(CachedStorage<>), () => new CachedStorage<TestStorageObject>(new MemoryStorage<TestStorageObject>(), new MemoryStorage<TestStorageObject>(), new MemoryStorage<CachedStorageItems<TestStorageObject>>()));
        }

        [Fact]
        public async Task it_should_put_deleted_items_into_recycle_bin()
        {
            var recycleBin = new MemoryStorage<TestStorageObject>();
            var storage = new RecycledStorage<TestStorageObject>(new MemoryStorage<TestStorageObject>(), recycleBin);

            await storage.Put(new TestStorageObject("1"));
            Assert.Null(await recycleBin.Get("1"));
            await storage.Delete("1");

            var deleted = await recycleBin.Get("1");
            Assert.NotNull(deleted);
            Assert.Equal("1", deleted.Id);
        }
    }
}
