namespace Nine.Storage
{
    using System.Diagnostics;
    using Xunit;

    public class AzureStoragePartitionKeyTest
    {
        [Theory]
        [InlineData("abcdefg", 32, 4, 26)]
        public void batch_storage_key_to_partition_key(string key, int partitionCount, int partitionKeyLength, int expectedPartition)
        {
            var partition = BatchedTableStorage<TestStorageObject>.GetPartitionKey(key, partitionCount, partitionKeyLength);
            Trace.WriteLine($"{ key } => { partition }");
            Assert.Equal(expectedPartition, partition);
        }
    }
}
