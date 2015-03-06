namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;

    class BlobStorageTest : BlobStorageSpec<BlobStorageTest>
    {
        public override IEnumerable<Func<IBlobStorage>> GetData()
        {
            return new[]
            {
                new Func<IBlobStorage>(() => new BlobStorage("DefaultEndpointsProtocol=https;AccountName=ninetest;AccountKey=ICfPLTxuKmhIj6XXSdWF4XPHByeXc4POIFpZLuo1EgMCJHpDVnSBEfaxBhV6P/eQ3yxEeGUW6um+VTCIOm1rpQ==")),
            };
        }
    }
}
