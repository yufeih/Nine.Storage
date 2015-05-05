namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Nine.Formatting;

    public class SqlStorage<T> : IDisposable, IStorage<T> where T : class, IKeyed, new()
    {
        private static readonly Dictionary<string, SqlDbType> columns;

        static SqlStorage()
        {
            var properties =
                from p in typeof(T).GetTypeInfo().DeclaredProperties
                where p.GetMethod != null && p.GetMethod.IsPublic &&
                      p.SetMethod != null && p.SetMethod.IsPublic &&
                      p.GetIndexParameters().Length <= 0
                select new { Name = p.Name, Type = p.PropertyType };

            var fields = 
                from f in typeof(T).GetTypeInfo().DeclaredFields
                where f.IsPublic
                select new { Name = f.Name, Type = f.FieldType };

            columns = properties.Concat(fields).ToDictionary(m => m.Name, m => GetDbType(m.Type), StringComparer.OrdinalIgnoreCase);
        }

        private static SqlDbType GetDbType(Type type)
        {
            if (type == typeof(string)) return SqlDbType.NVarChar;
            if (type == typeof(bool)) return SqlDbType.Bit;
            if (type == typeof(int)) return SqlDbType.Int;
            if (type == typeof(uint)) return SqlDbType.Int;
            if (type == typeof(short)) return SqlDbType.SmallInt;
            if (type == typeof(ushort)) return SqlDbType.SmallInt;
            if (type == typeof(long)) return SqlDbType.BigInt;
            if (type == typeof(ulong)) return SqlDbType.BigInt;
            if (type.IsEnum) return SqlDbType.Int;

            return SqlDbType.NVarChar;
        }
        
        private static string ToText(SqlDbType type)
        {
            if (type == SqlDbType.NVarChar) return "nvarchar(max)";
            return type.ToString().ToLowerInvariant();
        }

        private readonly TextConverter converter;
        private readonly SqlConnection connection;
        private readonly string tableName;

        public SqlStorage(string connectionString, string tableName = null, bool autoSchema = false, TextConverter converter = null)
        {
            if (string.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));
            
            this.connection = new SqlConnection(connectionString);
            this.connection.Open();
            this.tableName = tableName ?? typeof(T).Name;
            this.converter = converter;

            using (var reader = CreateTableIfNotExist())
            {
                if (autoSchema)
                {
                    UpgradeSchema(reader);
                }
            }
        }

        private SqlDataReader CreateTableIfNotExist()
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = $"select top 0 * from { tableName }";

                try
                {
                    return command.ExecuteReader();
                }
                catch (SqlException) { }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = $"create table { tableName } ({ string.Join(", ", columns.Select(c => $"{ c.Key } { ToText(c.Value) }")) })";
                return command.ExecuteReader();
            }
        }

        private void UpgradeSchema(SqlDataReader reader)
        {
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                var type = reader.GetFieldType(i);

                throw new NotImplementedException();
            }
        }

        public Task<bool> Add(T value)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "insert into values";
                return Task.FromResult(command.ExecuteNonQuery() == 1);
            }
        }

        public Task<bool> Delete(string key)
        {
            throw new NotImplementedException();
        }

        public Task<T> Get(string key)
        {
            throw new NotImplementedException();
        }

        public Task Put(T value)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<T>> Range(string minKey, string maxKey, int? count = default(int?))
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            var disposable = connection as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }
        }
    }
}
