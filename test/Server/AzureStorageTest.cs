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
            if (string.IsNullOrEmpty(Connection.Current.AzureStorage)) yield break;

            yield return new TestFactory<IStorage<TestStorageObject>>(
                typeof(BatchedTableStorage<>),
                () => new BatchedTableStorage<TestStorageObject>(
                    Connection.Current.AzureStorage,
                    "BatchedTableStorage" + Environment.TickCount.ToString()));

            yield return new TestFactory<IStorage<TestStorageObject>>(
                typeof(TableStorage<>),
                () => new TableStorage<TestStorageObject>(
                    Connection.Current.AzureStorage, 
                    "TableStorage" + Environment.TickCount.ToString()));
        }
    }

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
