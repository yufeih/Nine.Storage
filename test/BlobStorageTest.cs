namespace Nine.Storage.Blobs
{
    using System.Collections.Generic;
    using Xunit;

    public class BlobStorageTest : BlobStorageSpec<BlobStorageTest>
    {
        public override IEnumerable<ITestFactory<IBlobStorage>> GetData()
        {
            yield return new TestFactory<IBlobStorage>(nameof(MemoryBlobStorage), () => new MemoryBlobStorage());
            yield return new TestFactory<IBlobStorage>(nameof(MemoryBlobStorage), () => new ContentAddressableStorage(new MemoryBlobStorage()));

            if (!string.IsNullOrEmpty(Connection.Current.AzureStorage))
            {
                yield return new TestFactory<IBlobStorage>(nameof(AzureBlobStorage), () => new AzureBlobStorage(Connection.Current.AzureStorage) { Cache = true });
                yield return new TestFactory<IBlobStorage>(nameof(AzureBlobStorage), () => new AzureBlobStorage(Connection.Current.AzureStorage) { Cache = false });
            }
        }
    }
}
