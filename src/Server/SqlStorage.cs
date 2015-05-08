namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Nine.Formatting;

    public class SqlStorage<T> : IDisposable, IStorage<T> where T : class, IKeyed, new()
    {
        private static readonly List<SqlColumn> columns = SqlColumn.FromType(typeof(T)).ToList();

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
                    UpgradeSchema(reader.GetSchemaTable());
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
                catch (SqlException e) when (e.ErrorCode == -2146232060) { } // 0x80131904
            }

            using (var command = connection.CreateCommand())
            {
                var columnText = $"{ string.Concat(columns.Select(c => $"{ c.Name } { c.ToDbTypeText() }, ")) }";
                var constraint = $"constraint [PK_{ tableName }] primary key ({ SqlColumn.KeyColumnName })";
                command.CommandText = $"create table { tableName } ({ columnText } { constraint })";
                return command.ExecuteReader();
            }
        }

        private void UpgradeSchema(DataTable schema)
        {
            var columnsToAdd = columns.ToList();
            var schemaColumnNames = schema.Columns.OfType<DataColumn>().Select(c => c.ColumnName).ToList();

            columnsToAdd.RemoveAll(c => schemaColumnNames.Contains(c.Name));

            if (columnsToAdd.Count > 0)
            {
                throw new NotImplementedException();
            }
        }

        public async Task<bool> Add(T value)
        {
            using (var command = connection.CreateCommand())
            {
                var sb = StringBuilderCache.Acquire(260);
                sb.Append("insert into ");
                sb.Append(tableName);
                sb.Append(" (");

                for (var i = 0; i < columns.Count; i++)
                {
                    sb.Append(columns[i].Name);
                    if (i != columns.Count - 1) sb.Append(',');
                }
                sb.Append(") values(");

                for (var i = 0; i < columns.Count; i++)
                {
                    sb.Append("@");
                    sb.Append(i);
                    if (i != columns.Count - 1) sb.Append(',');
                }
                sb.Append(")");

                command.CommandText = StringBuilderCache.GetStringAndRelease(sb);

                for (int i = 0; i < columns.Count; i++)
                {
                    var columnValue = columns[i].ToSqlValue(value, converter);
                    command.Parameters.AddWithValue("@" + i, columnValue);
                }

                try
                {
                    return await command.ExecuteNonQueryAsync().ConfigureAwait(false) == 1;
                }
                catch (SqlException e) when (e.ErrorCode == -2146232060) // 0x80131904
                {
                    return false;
                }
            }
        }

        public Task<bool> Delete(string key)
        {
            throw new NotImplementedException();
        }

        public async Task<T> Get(string key)
        {
            using (var command = connection.CreateCommand())
            {
                var sb = StringBuilderCache.Acquire(260);
                sb.Append("select * from ");
                sb.Append(tableName);
                sb.Append(" where ");
                sb.Append(SqlColumn.KeyColumnName);
                sb.Append(" = @key");

                command.CommandText = StringBuilderCache.GetStringAndRelease(sb);
                command.Parameters.AddWithValue("@key", key);

                try
                {
                    await command.ExecuteReaderAsync().ConfigureAwait(false);
                    return null;
                }
                catch (SqlException e) when (e.ErrorCode == -2146232060) // 0x80131904
                {
                    return null;
                }
            }
        }

        public Task Put(T value)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "insert into values";
                return Task.FromResult(command.ExecuteNonQuery() == 1);
            }
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
