namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Xunit;

    public class AzureStorageTest : StorageSpec<AzureStorageTest>
    {
        public override IEnumerable<ITestFactory<IStorage<TestStorageObject>>> GetData()
        {
            if (!string.IsNullOrEmpty(Connection.Current.AzureStorage))
            {
                // TODO: BatchedTableStorage is not flushed during the test.
                yield return new TestFactory<IStorage<TestStorageObject>>(
                    typeof(AzureTableBatchStorage<>),
                    () => new AzureTableBatchStorage<TestStorageObject>(
                        Connection.Current.AzureStorage,
                        "BatchedTableStorage" + Guid.NewGuid().ToString("N")));

                yield return new TestFactory<IStorage<TestStorageObject>>(
                    typeof(AzureTableStorage<>),
                    () => new AzureTableStorage<TestStorageObject>(
                        Connection.Current.AzureStorage,
                        "TableStorage" + Guid.NewGuid().ToString("N")));
            }

            yield return new TestFactory<IStorage<TestStorageObject>>("dummy", () => new MemoryStorage<TestStorageObject>());
        }
    }

    public class AzureStoragePartitionKeyTest
    {
        [Theory]
        [InlineData("abcdefg", 32, 4, 26)]
        public void batch_storage_key_to_partition_key(string key, int partitionCount, int partitionKeyLength, int expectedPartition)
        {
            var partition = AzureTableBatchStorage<TestStorageObject>.GetPartitionKey(key, partitionCount, partitionKeyLength);
            Trace.WriteLine($"{ key } => { partition }");
            Assert.Equal(expectedPartition, partition);
        }
    }
}
