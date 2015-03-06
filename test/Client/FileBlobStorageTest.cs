namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;

    public class FileBlobStorageTest : BlobStorageSpec<FileBlobStorageTest>
    {
        public override IEnumerable<Func<IBlobStorage>> GetData()
        {
            return new[]
            {
                new Func<IBlobStorage>(() => new MemoryBlobStorage()),
                new Func<IBlobStorage>(() => new FileBlobStorage()),
                new Func<IBlobStorage>(() => new FileBlobStorage("Nine.BlobStorageTest")),
            };
        }
    }
}
