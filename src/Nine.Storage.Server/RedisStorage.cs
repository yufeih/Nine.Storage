namespace Nine.Storage
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Nine.Formatting;
    using StackExchange.Redis;

    public class RedisStorage<T> : IStorage<T>
    {
        private static readonly IFormatter formatter = new JsonFormatter();
        private static readonly ConcurrentDictionary<string, Lazy<ConnectionMultiplexer>> connections = new ConcurrentDictionary<string, Lazy<ConnectionMultiplexer>>();

        private readonly ConnectionMultiplexer redis;
        private readonly IDatabase db;
        private readonly string name;

        public RedisStorage(string connection, string name = null)
        {
            this.redis = connections.GetOrAdd(connection, x => new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(connection))).Value;
            this.db = redis.GetDatabase();
            this.name = (name ?? typeof(T).Name);
        }

        public async Task<T> Get(string key)
        {
            key = name + "/" + key;
            var value = await db.StringGetAsync(key);
            if (!value.HasValue) return default(T);
            return formatter.FromBytes<T>(value);
        }

        public async Task<IEnumerable<T>> Range(string minKey = null, string maxKey = null, int? count = null)
        {
            if (minKey != null) minKey = name + "/" + minKey;
            if (maxKey != null) maxKey = name + "/" + maxKey;

            var keys = await db.SortedSetRangeByValueAsync(name, minKey, maxKey, Exclude.Stop, 0, count.HasValue ? count.Value : -1);
            if (keys == null || keys.Length <= 0) return Enumerable.Empty<T>();

            return from value in await db.StringGetAsync((from x in keys select (RedisKey)x.ToString()).ToArray())
                   select formatter.FromBytes<T>(value);
        }

        public async Task<bool> Add(string key, T value)
        {
            key = name + "/" + key;
            if (!await db.SortedSetAddAsync(name, key, 0)) return false;
            await db.StringSetAsync(key, formatter.ToBytes(value));
            return true;
        }

        public async Task Put(string key, T value)
        {
            key = name + "/" + key;
            await db.StringSetAsync(key, formatter.ToBytes(value));
            await db.SortedSetAddAsync(name, key, 0);
        }

        public Task<bool> Delete(string key)
        {
            key = name + "/" + key;
            return db.SortedSetRemoveAsync(name, key);
        }
    }
}
