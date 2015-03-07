namespace Nine.Storage
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
                new TestFactory<IBlobStorage>(nameof(BlobStorage), () => new BlobStorage("")),
            };
        }
    }
}
