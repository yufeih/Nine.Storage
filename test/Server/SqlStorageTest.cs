namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ProtoBuf;
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
                        "TestStorageObject" + (int.MaxValue - Environment.TickCount).ToString()));
            }
            else
            {
                yield return new TestFactory<IStorage<TestStorageObject>>("dummy", () => new MemoryStorage<TestStorageObject>());
            }
        }

        class TestStorageObject1 : IKeyed
        {
            [ProtoMember(1)]
            public string Id { get; set; }
            [ProtoMember(2)]
            public string Name { get; set; }

            public string GetKey() => Id;
        }

        [Fact]
        public async Task add_new_column_when_poco_is_upgraded()
        {
            if (string.IsNullOrEmpty(Connection.Current.Sql)) return;

            var name = "TestStorageObject" + (int.MaxValue - Environment.TickCount).ToString();
            var oldStorage = new SqlStorage<TestStorageObject1>(Connection.Current.Sql, name, true);
            await oldStorage.Put(new TestStorageObject1 { Id = "1", Name = "n1" });

            var newStorage = new SqlStorage<TestStorageObject>(Connection.Current.Sql, name, true);
            await oldStorage.Put(new TestStorageObject1 { Id = "2", Name = "n2" });
            await newStorage.Put(new TestStorageObject { Id = "3", Name = "n3", Enum = StringComparison.InvariantCulture });

            Assert.Collection(await newStorage.Range(null, null),
                x => Assert.Equal("n1", x.Name),
                x => Assert.Equal("n2", x.Name),
                x => Assert.Equal("n3", x.Name));

            Assert.Collection(await oldStorage.Range(null, null),
                x => Assert.Equal("n1", x.Name),
                x => Assert.Equal("n2", x.Name),
                x => Assert.Equal("n3", x.Name));

            Assert.Equal(StringComparison.InvariantCulture, (await newStorage.Get("3")).Enum);
        }
    }
}
