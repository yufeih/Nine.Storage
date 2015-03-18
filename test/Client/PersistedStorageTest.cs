namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using Xunit;

    public class PersistedStorageTest : StorageSpec<PersistedStorageTest>
    {
        public override IEnumerable<ITestFactory<IStorage<TestStorageObject>>> GetData()
        {
            return new[]
            {
                new TestFactory<IStorage<TestStorageObject>>(typeof(PersistedStorage<>), () => new PersistedStorage<TestStorageObject>(Guid.NewGuid().ToString())),
            };
        }
    }
}
