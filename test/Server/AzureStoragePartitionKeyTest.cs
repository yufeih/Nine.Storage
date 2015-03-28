namespace Nine.Storage
{
    using System.Diagnostics;
    using Xunit;

    public class AzureStoragePartitionKeyTest
    {
        [Theory]
        public void batch_storage_key_to_partition_key(string key, int partitionCount, int partitionKeyLength)
        {
            var partition = BatchedTableStorage<TestStorageObject>.GetPartitionKey(key, partitionCount, partitionKeyLength);
            Trace.WriteLine($"{ key } => { partition }");
        }
    }
}
