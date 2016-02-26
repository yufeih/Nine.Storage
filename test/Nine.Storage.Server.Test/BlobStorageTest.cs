namespace Nine.Storage.Blobs
{
    using System;
    using System.Collections.Generic;
    using Xunit;

    class BlobStorageTest : BlobStorageSpec<BlobStorageTest>
    {
        public override IEnumerable<ITestFactory<IBlobStorage>> GetData()
        {
            return new[]
            {
                new TestFactory<IBlobStorage>(nameof(AzureBlobStorage), () => new AzureBlobStorage("") { Cache = true }),
                new TestFactory<IBlobStorage>(nameof(AzureBlobStorage), () => new AzureBlobStorage("") { Cache = false }),
            };
        }
    }
}
