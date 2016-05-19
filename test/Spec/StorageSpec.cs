namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Xunit;

    public abstract class StorageSpec<TData> : ITestFactoryData<IStorage<TestStorageObject>> where TData : ITestFactoryData<IStorage<TestStorageObject>>, new()
    {
        public static IEnumerable<object[]> Data = new TestFactoryDimension<TData, IStorage<TestStorageObject>>();

        public abstract IEnumerable<ITestFactory<IStorage<TestStorageObject>>> GetData();

        [Theory, MemberData("Data")]
        public async Task get_an_not_existing_entity_should_return_null(ITestFactory<IStorage<TestStorageObject>> storageFactory)
        {
            var storage = storageFactory.Create();
            var id = Guid.NewGuid().ToString("N");
            Assert.Null(await storage.Get("asdf").ConfigureAwait(false));
        }

        [Theory, MemberData("Data")]
        public async Task basic_types_should_be_identical_after_saved_to_storage(ITestFactory<IStorage<TestStorageObject>> storageFactory)
        {
            var storage = storageFactory.Create();
            var expected = new TestStorageObject
            {
                ApplicationName = "Test",
                Id = Guid.NewGuid().ToString(),
                Enum = StringComparison.OrdinalIgnoreCase,
                NullableTime2 = DateTime.UtcNow,
            };

            await storage.Put(expected).ConfigureAwait(false);

            var actual = await storage.Get(expected.Id).ConfigureAwait(false);

            Assert.Equal(expected.Time3, actual.Time3);
            Assert.Equal(expected.NullableTime, actual.NullableTime);
            Assert.Equal(expected.NullableTime2, actual.NullableTime2);
            Assert.Equal(expected.Enum, actual.Enum);
        }

        [Theory, MemberData("Data")]
        public async Task add_get_and_remove_an_object_from_storage(ITestFactory<IStorage<TestStorageObject>> storageFactory)
        {
            var storage = storageFactory.Create();
            var id = Guid.NewGuid().ToString("N");
            await storage.Put(new TestStorageObject { Id = id, Name = "my" }).ConfigureAwait(false);

            var entity = await storage.Get(id).ConfigureAwait(false);

            Assert.Equal(id, entity.Id);
            Assert.Equal("my", entity.Name);

            await storage.Delete(id).ConfigureAwait(false);

            Assert.Null(await storage.Get(id).ConfigureAwait(false));
        }

        [Theory, MemberData("Data")]
        public async Task it_should_not_treat_datetime_like_string_as_datetime(ITestFactory<IStorage<TestStorageObject>> storageFactory)
        {
            var storage = storageFactory.Create();
            for (var i = 0; i < 1; i++)
            {
                await storage.Put(new TestStorageObject { Id = Guid.NewGuid().ToString("N"), ApplicationName = "2016-03-12T10:35:57.0478164Z" });
            }

            await storage.Put(new TestStorageObject { Id = Guid.NewGuid().ToString("N"), ApplicationName = "Booom!!!" });
        }

        [Theory, MemberData("Data")]
        public async Task remove_object_from_storage(ITestFactory<IStorage<TestStorageObject>> storageFactory)
        {
            var storage = storageFactory.Create();

            await storage.Put(new TestStorageObject { Id = "1111", Name = "my1" });
            await storage.Put(new TestStorageObject { Id = "2222", Name = "my2" });

            Assert.Equal("my1", (await storage.Get("1111").ConfigureAwait(false)).Name);
            Assert.Equal("my2", (await storage.Get("2222").ConfigureAwait(false)).Name);

            Assert.True(await storage.Delete("1111").ConfigureAwait(false));

            Assert.Null(await storage.Get("1111").ConfigureAwait(false));
            Assert.Equal("my2", (await storage.Get("2222").ConfigureAwait(false)).Name);

            await storage.Put(new TestStorageObject { Id = "1111", Name = "my3" }).ConfigureAwait(false);
            Assert.Equal("my3", (await storage.Get("1111").ConfigureAwait(false)).Name);

            storage = storageFactory.Create();

            await Task.WhenAll(Enumerable.Range(0, 10).Select(i => storage.Put(new TestStorageObject { Id = i.ToString(), Name = i.ToString() })));
            Assert.True((await Task.WhenAll(Enumerable.Range(2, 6).Select(i => storage.Delete(i.ToString())))).Distinct().Single());

            try
            {
                var names = (await storage.Range(null, null)).Select(x => x.Name);
                Assert.Equal(new[] { "0", "1", "8", "9" }, names);
            }
            catch (NotSupportedException)
            {

            }
        }

        [Theory, MemberData("Data")]
        public async Task try_add_using_the_same_key_should_fail(ITestFactory<IStorage<TestStorageObject>> storageFactory)
        {
            try
            {
                var storage = storageFactory.Create();

                Assert.True(await storage.Add(new TestStorageObject { Id = "tryadd", Name = "1" }).ConfigureAwait(false));
                Assert.False(await storage.Add(new TestStorageObject { Id = "tryadd", Name = "2" }).ConfigureAwait(false));

                Assert.Equal("1", (await storage.Get("tryadd").ConfigureAwait(false)).Name);

                await storage.Delete("tryadd").ConfigureAwait(false);
            }
            catch (NotSupportedException)
            {
            }
        }

        [Theory, MemberData("Data")]
        public async Task put_using_the_same_key_should_override(ITestFactory<IStorage<TestStorageObject>> storageFactory)
        {
            var storage = storageFactory.Create();

            await storage.Put(new TestStorageObject { Id = "put", Name = "1" }).ConfigureAwait(false);
            await storage.Put(new TestStorageObject { Id = "put", Name = "2" }).ConfigureAwait(false);

            Assert.Equal("2", (await storage.Get("put").ConfigureAwait(false)).Name);

            await storage.Delete("put").ConfigureAwait(false);
        }

        [Theory, MemberData("Data")]
        public async Task get_many_should_repect_range(ITestFactory<IStorage<TestStorageObject>> storageFactory)
        {
            var storage = storageFactory.Create();
            var users = Enumerable.Range(100, 4).Select(i => new TestStorageObject { Id = i.ToString(), Name = "user" + i }).ToArray();

            await Task.WhenAll(from user in users select storage.Put(user)).ConfigureAwait(false);

            try
            {
                var all = await storage.Range(null, null, null).ConfigureAwait(false);
                Assert.Equal(4, all.Count());
            }
            catch (NotSupportedException)
            {
            }

            try
            {
                var all = await storage.Range(null, null, 2).ConfigureAwait(false);
                Assert.Equal(2, all.Count());
            }
            catch (NotSupportedException)
            {
            }

            try
            {
                var min = await storage.Range("103", null, null).ConfigureAwait(false);
                Assert.Equal(1, min.Count());
            }
            catch (NotSupportedException)
            {
            }

            try
            {
                var min = await storage.Range("101", null, 2).ConfigureAwait(false);
                Assert.Equal(2, min.Count());
            }
            catch (NotSupportedException)
            {
            }

            try
            {
                var max = await storage.Range(null, "102", null).ConfigureAwait(false);
                Assert.Equal(2, max.Count());
            }
            catch (NotSupportedException)
            {
            }

            try
            {
                var max = await storage.Range(null, "103", 2).ConfigureAwait(false);
                Assert.Equal(2, max.Count());
            }
            catch (NotSupportedException)
            {
            }

            await Task.WhenAll(from user in users select storage.Delete(user.Id)).ConfigureAwait(false);
        }

        [Theory, MemberData("Data")]
        public async Task paged_query_with_get_many_async(ITestFactory<IStorage<TestStorageObject>> storageFactory)
        {
            try
            {
                var records = 0;
                var storage = storageFactory.Create();
                var users = Enumerable.Range(100, 4).Select(i => new TestStorageObject { Id = i.ToString(), Name = "user" + i }).ToArray();

                await Task.WhenAll(from user in users select storage.Put(user)).ConfigureAwait(false);

                string start = "100";

                Assert.Equal(4, (await storage.Range(null, null, null).ConfigureAwait(false)).Count());

                while (true)
                {
                    var page = await storage.Range(start, null, 2).ConfigureAwait(false);
                    if (page.Count() <= 0) break;

                    start = StorageKey.Increment(page.Last().Id);
                    records += page.Count();
                    Assert.Equal(2, page.Count());
                }

                Assert.Equal(4, records);

                await Task.WhenAll(from user in users select storage.Delete(user.Id)).ConfigureAwait(false);
            }
            catch (NotSupportedException)
            {
            }
        }

        [Theory, MemberData("Data")]
        public async Task stress_add_get_and_remove_many_objects_from_table_storage_in_parallel(ITestFactory<IStorage<TestStorageObject>> storageFactory)
        {
            var storage = storageFactory.Create();

            var count = 10;
            var stopwatch = new Stopwatch();
            var users = Enumerable.Range(0, count).Select(i => new TestStorageObject { Id = i.ToString(), Name = "user" + i }).ToArray();

            stopwatch.Start();
            await Task.WhenAll(from user in users select storage.Put(user)).ConfigureAwait(false);

            stopwatch.Start();
            try
            {
                var items = (await storage.Range(null, null, null)).ToList();

                items.Sort((a, b) => int.Parse(a.Id) - int.Parse(b.Id));

                for (int i = 0; i < items.Count; i++)
                {
                    Assert.Equal(i.ToString(), items[i].Id);
                    Assert.Equal("user" + i, items[i].Name);
                }
            }
            catch (NotSupportedException)
            {
            }

            stopwatch.Start();
            await Task.WhenAll(from user in users select storage.Delete(user.Id)).ConfigureAwait(false);
        }
    }
}
