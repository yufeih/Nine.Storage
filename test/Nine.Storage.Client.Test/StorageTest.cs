namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using SQLite.Net.Platform.Win32;
    using Xunit;
    using Nine.Storage.Caching;

    public class StorageTest : StorageSpec<StorageTest>
    {
        public override IEnumerable<ITestFactory<IStorage<TestStorageObject>>> GetData()
        {
            return new[]
            {
                // TODO: add it back when we know how to copy native binaries on dnx project
                // new TestFactory<IStorage<TestStorageObject>>(typeof(SqliteStorage<>), () => new SqliteStorage<TestStorageObject>($"sqlite-{ Environment.TickCount }.db", new SQLitePlatformWin32())),
                new TestFactory<IStorage<TestStorageObject>>(typeof(MemoryStorage<>), () => new MemoryStorage<TestStorageObject>()),
                new TestFactory<IStorage<TestStorageObject>>(typeof(MemoryStorage<>), () => new MemoryStorage<TestStorageObject>(true)),
                new TestFactory<IStorage<TestStorageObject>>(typeof(RecycledStorage<>), () => new RecycledStorage<TestStorageObject>(new MemoryStorage<TestStorageObject>(), new MemoryStorage<TestStorageObject>())),
                new TestFactory<IStorage<TestStorageObject>>(typeof(CachedStorage<>), () => new CachedStorage<TestStorageObject>(new MemoryStorage<TestStorageObject>(), new MemoryStorage<TestStorageObject>())),
                new TestFactory<IStorage<TestStorageObject>>(typeof(CachedStorage<>), () => new CachedStorage<TestStorageObject>(new MemoryStorage<TestStorageObject>(), new MemoryStorage<TestStorageObject>(), new MemoryStorage<CachedStorageItems<TestStorageObject>>())),
            };
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
