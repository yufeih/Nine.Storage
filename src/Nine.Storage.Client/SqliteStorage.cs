namespace Nine.Storage
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Nine.Formatting;
    using SQLite.Net;
    using SQLite.Net.Interop;
    using SQLite.Net.Attributes;
    using System;

    public class SqliteStorage<T> : IStorage<T> where T : class, IKeyed, new()
    {
        private const SQLiteOpenFlags SqliteOpenFlags = SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.FullMutex;

        private readonly SQLiteConnection _db;
        private readonly IFormatter _formatter;
        private readonly object _lock = new object();

        class Table
        {
            [PrimaryKey]
            public string Key { get; set; }
            public byte[] Value { get; set; }
        }

        public SqliteStorage(string databasePath, ISQLitePlatform platform, IFormatter formatter = null)
        {
            _formatter = formatter ?? new JsonFormatter();
            _db = new SQLiteConnection(platform, databasePath, SqliteOpenFlags);
            _db.CreateTable<Table>();
        }

        public Task<bool> Add(T value)
        {
            lock (_lock)
            {
                try
                {
                    return Task.FromResult(_db.Insert(new Table { Key = value.GetKey(), Value = _formatter.ToBytes(value) }) == 1);
                }
                catch (SQLiteException)
                {
                    return Task.FromResult(false);
                }
            }
        }

        public Task<bool> Delete(string key)
        {
            lock (_lock)
            {
                return Task.FromResult(_db.Delete<Table>(key) == 1);
            }
        }

        public Task<T> Get(string key)
        {
            lock (_lock)
            {
                var query = "select * from \"Table\" where \"Key\" = ?";
                var bytes = _db.Query<Table>(query, key).SingleOrDefault()?.Value;
                return Task.FromResult(bytes != null ? _formatter.FromBytes<T>(bytes) : null);
            }
        }

        public Task Put(T value)
        {
            lock (_lock)
            {
                return Task.FromResult(_db.InsertOrReplace(new Table { Key = value.GetKey(), Value = _formatter.ToBytes(value) }) == 1);
            }
        }

        public Task<IEnumerable<T>> Range(string minKey, string maxKey, int? count = default(int?))
        {
            lock (_lock)
            {
                var limit = new Func<string, string>(x => count != null ? x += " limit " + count.Value : x);

                List<Table> result = null;

                if (string.IsNullOrEmpty(minKey))
                {
                    if (string.IsNullOrEmpty(maxKey))
                    {
                        result = _db.Query<Table>(limit("select * from \"Table\""));
                    }
                    else
                    {
                        result = _db.Query<Table>(limit("select * from \"Table\" where \"Key\" < ?"), maxKey);
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(maxKey))
                    {
                        result = _db.Query<Table>(limit("select * from \"Table\" where \"Key\" >= ?"), minKey);
                    }
                    else
                    {
                        result = _db.Query<Table>(limit("select * from \"Table\" where \"Key\" >= ? and \"Key\" < ?"), minKey, maxKey);
                    }
                }

                return Task.FromResult(result.Select(row => _formatter.FromBytes<T>(row.Value)));
            }
        }
    }
}
