namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Nine.Formatting;
    using SQLite.Net;
    using SQLite.Net.Attributes;
    using SQLite.Net.Interop;

    public class SqliteStorage<T> : IStorage<T>
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

        public SqliteStorage(string databasePath, ISQLitePlatform platform, IFormatter formatter)
        {
            if (formatter == null) throw new ArgumentNullException(nameof(formatter));
            
            _formatter = formatter;
            _db = new SQLiteConnection(platform, databasePath, SqliteOpenFlags);
            _db.CreateTable<Table>();
        }

        public Task<bool> Add(string key, T value)
        {
            lock (_lock)
            {
                try
                {
                    var ms = new MemoryStream();
                    _formatter.WriteTo(value, ms);
                    return _db.Insert(new Table { Key = key, Value = ms.ToArray() }) == 1 ? Tasks.True : Tasks.False;
                }
                catch (SQLiteException)
                {
                    return Tasks.False;
                }
            }
        }

        public Task<bool> Delete(string key)
        {
            lock (_lock)
            {
                return _db.Delete<Table>(key) == 1 ? Tasks.True : Tasks.False;
            }
        }

        public Task<T> Get(string key)
        {
            lock (_lock)
            {
                var query = "select * from \"Table\" where \"Key\" = ?";
                var bytes = _db.Query<Table>(query, key).SingleOrDefault()?.Value;
                return Task.FromResult(bytes != null ? _formatter.ReadFrom<T>(new MemoryStream(bytes, writable: false)) : default(T));
            }
        }

        public Task Put(string key, T value)
        {
            lock (_lock)
            {
                var ms = new MemoryStream();
                _formatter.WriteTo(value, ms);
                _db.InsertOrReplace(new Table { Key = key, Value = ms.ToArray() });
                return Tasks.Completed;
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

                return Task.FromResult(result.Select(row => _formatter.ReadFrom<T>(new MemoryStream(row.Value, writable: false))));
            }
        }
    }
}
