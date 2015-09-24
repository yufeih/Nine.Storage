namespace Nine.Storage
{
    using System.Collections.Generic;
    using Xunit;

    public class FileBlobStorageTest : BlobStorageSpec<FileBlobStorageTest>
    {
        public override IEnumerable<ITestFactory<IBlobStorage>> GetData()
        {
            return new[]
            {
                new TestFactory<IBlobStorage>(nameof(MemoryBlobStorage), () => new MemoryBlobStorage()),
                new TestFactory<IBlobStorage>(nameof(FileBlobStorage), () => new FileBlobStorage("obj/blobs")),
            };
        }
    }
}
