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
        private readonly SQLiteConnection db;
        private readonly IFormatter formatter;

        class Table
        {
            [PrimaryKey]
            public string Key { get; set; }
            public byte[] Value { get; set; }
        }

        public SqliteStorage(string databasePath, ISQLitePlatform platform, IFormatter formatter = null)
        {
            this.formatter = formatter ?? new JsonFormatter();
            this.db = new SQLiteConnection(platform, databasePath);
            this.db.CreateTable<Table>();
        }

        public Task<bool> Add(T value)
        {
            try
            {
                return Task.FromResult(db.Insert(new Table { Key = value.GetKey(), Value = formatter.ToBytes(value) }) == 1);
            }
            catch (SQLiteException)
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> Delete(string key)
        {
            return Task.FromResult(db.Delete<Table>(key) == 1);
        }

        public Task<T> Get(string key)
        {
            var query = "select * from \"Table\" where \"Key\" = ?";
            var bytes = db.Query<Table>(query, key).SingleOrDefault()?.Value;
            return Task.FromResult(bytes != null ? formatter.FromBytes<T>(bytes) : null);
        }

        public Task Put(T value)
        {
            return Task.FromResult(db.InsertOrReplace(new Table { Key = value.GetKey(), Value = formatter.ToBytes(value) }) == 1);
        }

        public Task<IEnumerable<T>> Range(string minKey, string maxKey, int? count = default(int?))
        {
            var limit = new Func<string, string>(x => count != null ? x += " limit " + count.Value : x);

            List<Table> result = null;

            if (string.IsNullOrEmpty(minKey))
            {
                if (string.IsNullOrEmpty(maxKey))
                {
                    result = db.Query<Table>(limit("select * from \"Table\""));
                }
                else
                {
                    result = db.Query<Table>(limit("select * from \"Table\" where \"Key\" < ?"), maxKey);
                }
            }
            else
            {
                if (string.IsNullOrEmpty(maxKey))
                {
                    result = db.Query<Table>(limit("select * from \"Table\" where \"Key\" >= ?"), minKey);
                }
                else
                {
                    result = db.Query<Table>(limit("select * from \"Table\" where \"Key\" >= ? and \"Key\" < ?"), minKey, maxKey);
                }
            }

            return Task.FromResult(result.Select(row => formatter.FromBytes<T>(row.Value)));
        }
    }
}
