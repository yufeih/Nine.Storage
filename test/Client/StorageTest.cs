namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;

    public class CachedStorageTest : StorageSpec<CachedStorageTest>
    {
        public override IEnumerable<Func<IStorage<TestStorageObject>>> GetData()
        {
            return new[]
            {
                new Func<IStorage<TestStorageObject>>(() => new MemoryStorage<TestStorageObject>()),
                new Func<IStorage<TestStorageObject>>(() => new CachedStorage<TestStorageObject>(new MemoryStorage<TestStorageObject>(), new MemoryStorage<TestStorageObject>())),
                new Func<IStorage<TestStorageObject>>(() => new CachedStorage<TestStorageObject>(new MemoryStorage<TestStorageObject>(), new MemoryStorage<TestStorageObject>(), new MemoryStorage<CachedStorageItems<TestStorageObject>>())),
            };
        }
    }
}
