namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using Xunit;

    public class SqlStorageTest : StorageSpec<SqlStorageTest>
    {
        public override IEnumerable<ITestFactory<IStorage<TestStorageObject>>> GetData()
        {
            if (!string.IsNullOrEmpty(Connection.Current.Sql))
            {
                yield return new TestFactory<IStorage<TestStorageObject>>(
                    typeof(SqlStorage<>),
                    () => new SqlStorage<TestStorageObject>(
                        Connection.Current.Sql,
                        "TestStorageObject" + Environment.TickCount.ToString()));
            }
            else
            {
                yield return new TestFactory<IStorage<TestStorageObject>>("dummy", () => new MemoryStorage<TestStorageObject>());
            }
        }
    }
}
