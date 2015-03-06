namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Xunit;

    public class CacheTest : CacheSpec<CacheTest>
    {
        public override IEnumerable<Func<ICache<TestStorageObject>>> GetData()
        {
            yield return new Func<ICache<TestStorageObject>>(() => new MemoryStorage<TestStorageObject>());
        }

        [Fact]
        public async Task it_should_cache_ranged_values_with_a_common_prefix()
        {
            var persisted = new MemoryStorage<TestStorageObject>();
            var cache = new MemoryStorage<TestStorageObject>();
            var rangeCache = new MemoryStorage<CachedStorageItems<TestStorageObject>>();

            var storage = new CachedStorage<TestStorageObject>(persisted, cache, rangeCache);
            var objs = Enumerable.Range(0, 4).Select(i => new TestStorageObject("test" + i)).ToList();
            await Task.WhenAll(objs.Select(o => storage.Add(o)));

            var aaa = new TestStorageObject("testaaa");
            Assert.Equal(objs, await storage.List("test"));
            await persisted.Add(aaa);
            Assert.Equal(objs, await storage.List("test"));

            objs.RemoveAll(x => x.GetKey() == "test0");
            objs.Add(aaa);
            objs.Add(new TestStorageObject("testbbb"));
            objs.Add(new TestStorageObject("testccc"));

            await storage.Delete(new TestStorageObject("test0"));
            await storage.Add(objs[objs.Count - 1]);
            await storage.Add(objs[objs.Count - 2]);
            Assert.Equal(objs, await storage.List("test"));

            await storage.Add(new TestStorageObject("tesu"));
            await storage.Add(new TestStorageObject("tess"));
            Assert.Equal(objs, await storage.List("test"));
        }
    }
}
