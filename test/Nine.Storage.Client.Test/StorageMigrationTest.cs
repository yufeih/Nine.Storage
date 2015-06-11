namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Xunit;
    using ProtoBuf;
    
    class StorageMigrationTest
    {
        [ProtoContract]
        public class Migration : IKeyed
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public DateTime Time { get; set; }

            public string GetKey()
            {
                return StorageKey.Get(Id);
            }
        }

        [Fact]
        public static async Task storage_can_be_migrated_in_a_batch()
        {
            var v1 = new MemoryStorage<Migration>();
            var v2 = new MemoryStorage<Migration>();
            var v3 = new MemoryStorage<Migration>();
            var migrate = new StorageMigration<Migration>(v2, v1);
            var migrateAndDelete = new StorageMigration<Migration>(v3, v1, null, true);

            var data = Enumerable.Range(0, 100).Select(x => new Migration { Id = x, Time = DateTime.UtcNow });
            await Task.WhenAll(from x in data select v1.Put(x)).ConfigureAwait(false);

            Assert.True(data.Select(x => x.Id).SequenceEqual((await v1.All().ToListAsync().ConfigureAwait(false)).Select(x => x.Id)));

            await migrate.MigrateAsync(123).ConfigureAwait(false);

            Assert.True(data.Select(x => x.Id).SequenceEqual((await v1.All().ToListAsync().ConfigureAwait(false)).Select(x => x.Id)));
            Assert.True(data.Select(x => x.Id).SequenceEqual((await v2.All().ToListAsync().ConfigureAwait(false)).Select(x => x.Id)));

            await migrateAndDelete.MigrateAsync(456).ConfigureAwait(false);
            Assert.Equal(0, (await v1.All().ToListAsync()).Count());
            Assert.True(data.Select(x => x.Id).SequenceEqual((await v3.All().ToListAsync().ConfigureAwait(false)).Select(x => x.Id)));
        }

        [Fact]
        public static async Task storage_can_be_migrated_by_get_request()
        {
            var v1 = new MemoryStorage<Migration>();
            var v2 = new MemoryStorage<Migration>();
            var migrate = new StorageMigration<Migration>(v2, v1, null, true);

            await v1.Put(new Migration { Id = 1, Time = DateTime.UtcNow }).ConfigureAwait(false);

            Assert.Null(await v2.Get(StorageKey.Get(1)).ConfigureAwait(false));

            Assert.Equal(
                await v1.Get(StorageKey.Get(1)).ConfigureAwait(false),
                await migrate.Get(StorageKey.Get(1)).ConfigureAwait(false));

            Assert.Null(await v1.Get(StorageKey.Get(1)).ConfigureAwait(false));

            Assert.Equal(
                await v2.Get(StorageKey.Get(1)).ConfigureAwait(false),
                await migrate.Get(StorageKey.Get(1)).ConfigureAwait(false));
        }

        [Fact]
        public static async Task storage_can_be_migrated_by_get_many_request()
        {
            var v1 = new MemoryStorage<Migration>();
            var v2 = new MemoryStorage<Migration>();
            var migrate = new StorageMigration<Migration>(v2, v1, null, true);

            var data = Enumerable.Range(0, 1000).Select(x => new Migration { Id = x, Time = DateTime.UtcNow });
            await Task.WhenAll(from x in data select v1.Put(x)).ConfigureAwait(false);

            var a = await migrate.All().ToListAsync().ConfigureAwait(false);
            var b = await v2.All().ToListAsync().ConfigureAwait(false);

            Assert.True(a.SequenceEqual(b));
            Assert.False((await v1.All().ToListAsync().ConfigureAwait(false)).Any());
        }
    }
}
