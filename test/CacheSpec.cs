namespace Nine.Storage.Caching
{
    using System;
    using System.Collections.Generic;
    using Xunit;

    public abstract class CacheSpec<TData> : ITestFactoryData<ICache<TestStorageObject>> where TData : ITestFactoryData<ICache<TestStorageObject>>, new()
    {
        public static IEnumerable<object[]> Data = new TestFactoryDimension<TData, ICache<TestStorageObject>>();

        public abstract IEnumerable<ITestFactory<ICache<TestStorageObject>>> GetData();
        
        [Theory, MemberData("Data")]
        public void put_or_add_null_values(ITestFactory<ICache<TestStorageObject>> storageFactory)
        {
            TestStorageObject value;
            var storage = storageFactory.Create();
            var id = Guid.NewGuid().ToString("N");
            storage.Put(id, null);
            Assert.True(storage.TryGet(id, out value));
            Assert.Null(value);
        }
    }
}
