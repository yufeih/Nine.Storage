namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Xunit;

    public class StorageSharingTest : StorageSpec<StorageSharingTest>
    {
        public override IEnumerable<Func<IStorage<TestStorageObject>>> GetData()
        {
            return new[]
            {
                new Func<IStorage<TestStorageObject>>(() => new MemoryStorage<TestStorageObject>()),
                new Func<IStorage<TestStorageObject>>(() => PersistedStorage<TestStorageObject>.GetOrCreateAsync(Guid.NewGuid().ToString("N")).Result),
            };
        }

        [Theory, MemberData("Data")]
        public async Task should_support_shared_access(Func<IStorage<TestStorageObject>> storageFactory)
        {
            await Task.WhenAll(Enumerable.Range(0, 10).Select(x => add_get_and_remove_an_object_from_storage(storageFactory)));
        }
    }
}
