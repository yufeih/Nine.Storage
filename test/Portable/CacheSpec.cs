namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using Xunit;

    public abstract class CacheSpec<TData> : ITestData<Func<ICache<TestStorageObject>>> where TData : ITestData<Func<ICache<TestStorageObject>>>, new()
    {
        public static IEnumerable<object[]> Data = new TestDimension<TData, Func<ICache<TestStorageObject>>>();

        public abstract IEnumerable<Func<ICache<TestStorageObject>>> GetData();
        
        [Theory, MemberData("Data")]
        public void put_or_add_null_values(Func<ICache<TestStorageObject>> storageFactory)
        {
            TestStorageObject value;
            var storage = storageFactory();
            var id = Guid.NewGuid().ToString("N");
            storage.Put(id, null);
            Assert.True(storage.TryGet(id, out value));
            Assert.Null(value);
        }
    }
}
