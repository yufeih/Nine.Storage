namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Xunit;

    public class StorageSharingTest : StorageSpec<StorageSharingTest>
    {
        public override IEnumerable<ITestFactory<IStorage<TestStorageObject>>> GetData()
        {
            return new[]
            {
                new TestFactory<IStorage<TestStorageObject>>(typeof(MemoryStorage<>), () => new MemoryStorage<TestStorageObject>()),
                new TestFactory<IStorage<TestStorageObject>>(typeof(PersistedStorage<>), () => PersistedStorage<TestStorageObject>.GetOrCreateAsync(Guid.NewGuid().ToString("N")).Result),
            };
        }

        [Theory, MemberData("Data")]
        public async Task should_support_shared_access(ITestFactory<IStorage<TestStorageObject>> storageFactory)
        {
            await Task.WhenAll(Enumerable.Range(0, 10).Select(x => add_get_and_remove_an_object_from_storage(storageFactory)));
        }
    }
}
