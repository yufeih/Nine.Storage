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
        private static readonly ConcurrentDictionary<string, Lazy<ConnectionMultiplexer>> Connections = new ConcurrentDictionary<string, Lazy<ConnectionMultiplexer>>();

        private readonly ITextFormatter _formatter;
        private readonly ConnectionMultiplexer _redis;
        private readonly IDatabase _db;
        private readonly string _name;

        public RedisStorage(string connection, ITextFormatter formatter, string name = null)
        {
            if (formatter == null) throw new ArgumentNullException(nameof(formatter));
            
            _redis = Connections.GetOrAdd(connection, x => new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(connection))).Value;
            _db = _redis.GetDatabase();
            _name = (name ?? typeof(T).Name);
            _formatter = formatter;
        }

        public async Task<T> Get(string key)
        {
            key = string.Concat(_name, "/", key);
            var value = await _db.StringGetAsync(key);
            if (!value.HasValue) return default(T);
            return _formatter.FromText<T>(value);
        }

        public async Task<IEnumerable<T>> Range(string minKey = null, string maxKey = null, int? count = null)
        {
            if (minKey != null) minKey = string.Concat(_name, "/", minKey);
            if (maxKey != null) maxKey = string.Concat(_name, "/", maxKey);

            var keys = await _db.SortedSetRangeByValueAsync(_name, minKey, maxKey, Exclude.Stop, 0, count.HasValue ? count.Value : -1);
            if (keys == null || keys.Length <= 0) return Enumerable.Empty<T>();

            return from value in await _db.StringGetAsync((from x in keys select (RedisKey)x.ToString()).ToArray())
                   select _formatter.FromText<T>(value);
        }

        public async Task<bool> Add(string key, T value)
        {
            key = string.Concat(_name, "/", key);
            if (!await _db.SortedSetAddAsync(_name, key, 0)) return false;
            await _db.StringSetAsync(key, _formatter.ToText(value));
            return true;
        }

        public async Task Put(string key, T value)
        {
            key = string.Concat(_name, "/", key);
            await _db.StringSetAsync(key, _formatter.ToText(value));
            await _db.SortedSetAddAsync(_name, key, 0);
        }

        public Task<bool> Delete(string key)
        {
            key = string.Concat(_name, "/", key);
            return _db.SortedSetRemoveAsync(_name, key);
        }
    }
}
