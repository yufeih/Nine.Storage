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

    public class SqlStorage<T> : IStorage<T> where T : class, IKeyed, new()
    {
        private static readonly List<SqlColumn> columns = SqlColumn.FromType(typeof(T)).ToList();

        private readonly TextConverter converter;
        private readonly string connectionString;
        private readonly string tableName;

        public SqlStorage(string connectionString, string tableName = null, bool autoSchema = false, TextConverter converter = null)
        {
            if (string.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));

            this.connectionString = connectionString;
            this.tableName = tableName ?? typeof(T).Name;
            this.converter = converter;

            var schema = CreateTableIfNotExist();
            if (autoSchema)
            {
                UpgradeSchema(schema);
            }
        }

        private string[] CreateTableIfNotExist()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"select top 0 * from { tableName }";

                    try
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            return Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
                        }
                    }
                    catch (SqlException e) when (e.ErrorCode == -2146232060) { } // 0x80131904
                }
            }

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    var sb = StringBuilderCache.Acquire(260);

                    sb.Append("create table ");
                    sb.Append(tableName);
                    sb.Append(" (");

                    for (int i = 0; i < columns.Count; i++)
                    {
                        sb.Append(columns[i].Name);
                        sb.Append(" ");
                        sb.Append(columns[i].ToDbTypeText());
                        sb.Append(",");
                    }

                    sb.Append("constraint [PK_");
                    sb.Append(tableName);
                    sb.Append("] primary key (");
                    sb.Append(SqlColumn.KeyColumnName);
                    sb.Append("))");

                    command.CommandText = StringBuilderCache.GetStringAndRelease(sb);
                    command.ExecuteNonQuery();
                    return columns.Select(c => c.Name).ToArray();
                }
            }
        }

        private void UpgradeSchema(string[] columnNames)
        {
            var columnsToAdd = columns.Skip(1).ToList(); // Skip _Key

            columnsToAdd.RemoveAll(c => columnNames.Contains(c.Name));

            if (columnsToAdd.Count <= 0) return;

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    var sb = StringBuilderCache.Acquire(260);

                    for (int i = 0; i < columnsToAdd.Count; i++)
                    {
                        sb.Append("alter table ");
                        sb.Append(tableName);
                        sb.Append(" add ");
                        sb.Append(columnsToAdd[i].Name);
                        sb.Append(" ");
                        sb.Append(columnsToAdd[i].ToDbTypeText(true));
                        sb.Append(";");
                    }

                    command.CommandText = StringBuilderCache.GetStringAndRelease(sb);
                    command.ExecuteNonQuery();
                }
            }
        }

        public async Task<T> Get(string key)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    var sb = StringBuilderCache.Acquire(260);
                    sb.Append("select * from ");
                    sb.Append(tableName);
                    sb.Append(" where ");
                    sb.Append(SqlColumn.KeyColumnName);
                    sb.Append(" = @key");

                    command.CommandText = StringBuilderCache.GetStringAndRelease(sb);
                    command.Parameters.AddWithValue("@key", SqlColumn.ToBytes(key));

                    using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        if (!reader.Read()) return null;

                        var result = new T();
                        for (int i = 1; i < columns.Count; i++) // Skip the first '_Key' column
                        {
                            columns[i].FromSqlValue(result, reader, converter);
                        }
                        return result;
                    }
                }
            }
        }

        public async Task<bool> Add(T value)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
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
                        if (columnValue is DateTime)
                        {
                            // Ensure we are using DateTime2 to prevent precision loss
                            command.Parameters.Add("@" + i, SqlDbType.DateTime2).Value = columnValue;
                        }
                        else
                        {
                            command.Parameters.AddWithValue("@" + i, columnValue);
                        }
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
        }

        public async Task Put(T value)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    var sb = StringBuilderCache.Acquire(260);
                    sb.Append("merge ");
                    sb.Append(tableName);
                    sb.Append(" as target using (values (");

                    for (var i = 0; i < columns.Count; i++)
                    {
                        sb.Append("@");
                        sb.Append(i);
                        if (i != columns.Count - 1) sb.Append(',');
                    }

                    sb.Append(")) as source (");

                    for (var i = 0; i < columns.Count; i++)
                    {
                        sb.Append(columns[i].Name);
                        if (i != columns.Count - 1) sb.Append(',');
                    }
                    sb.Append(") on target.");
                    sb.Append(SqlColumn.KeyColumnName);
                    sb.Append(" = source.");
                    sb.Append(SqlColumn.KeyColumnName);

                    sb.Append(" when matched then update set ");

                    for (var i = 1; i < columns.Count; i++) // Do not update the _Key
                    {
                        sb.Append(columns[i].Name);
                        sb.Append(" = source.");
                        sb.Append(columns[i].Name);
                        if (i != columns.Count - 1) sb.Append(',');
                    }

                    sb.Append(" when not matched then insert (");
                    for (var i = 0; i < columns.Count; i++)
                    {
                        sb.Append(columns[i].Name);
                        if (i != columns.Count - 1) sb.Append(',');
                    }

                    sb.Append(") values (");
                    for (var i = 0; i < columns.Count; i++)
                    {
                        sb.Append(columns[i].Name);
                        if (i != columns.Count - 1) sb.Append(',');
                    }

                    sb.Append(");");

                    command.CommandText = StringBuilderCache.GetStringAndRelease(sb);

                    for (int i = 0; i < columns.Count; i++)
                    {
                        var columnValue = columns[i].ToSqlValue(value, converter);
                        if (columnValue is DateTime)
                        {
                            // Ensure we are using DateTime2 to prevent precision loss
                            command.Parameters.Add("@" + i, SqlDbType.DateTime2).Value = columnValue;
                        }
                        else
                        {
                            command.Parameters.AddWithValue("@" + i, columnValue);
                        }
                    }

                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        public async Task<bool> Delete(string key)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    var sb = StringBuilderCache.Acquire(260);
                    sb.Append("delete from ");
                    sb.Append(tableName);
                    sb.Append(" where ");
                    sb.Append(SqlColumn.KeyColumnName);
                    sb.Append(" = @key");

                    command.CommandText = StringBuilderCache.GetStringAndRelease(sb);
                    command.Parameters.AddWithValue("@key", SqlColumn.ToBytes(key));

                    return await command.ExecuteNonQueryAsync().ConfigureAwait(false) == 1;
                }
            }
        }

        public async Task<IEnumerable<T>> Range(string minKey, string maxKey, int? count = default(int?))
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    var hasMin = !string.IsNullOrEmpty(minKey);
                    var hasMax = !string.IsNullOrEmpty(maxKey);

                    var sb = StringBuilderCache.Acquire(260);
                    sb.Append("select");

                    if (count != null)
                    {
                        sb.Append(" top ");
                        sb.Append(count.Value);
                    }

                    sb.Append(" * from ");
                    sb.Append(tableName);

                    if (hasMin || hasMax)
                    {
                        sb.Append(" where ");

                        if (hasMin)
                        {
                            sb.Append(SqlColumn.KeyColumnName);
                            sb.Append(" >= @min");
                        }
                        if (hasMax)
                        {
                            if (hasMin) sb.Append(" and ");
                            sb.Append(SqlColumn.KeyColumnName);
                            sb.Append(" < @max");
                        }
                    }
                    sb.Append(" order by ");
                    sb.Append(SqlColumn.KeyColumnName);

                    command.CommandText = StringBuilderCache.GetStringAndRelease(sb);

                    if (hasMin) command.Parameters.AddWithValue("@min", SqlColumn.ToBytes(minKey));
                    if (hasMax) command.Parameters.AddWithValue("@max", SqlColumn.ToBytes(maxKey));

                    var result = new List<T>();
                    using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (reader.Read())
                        {
                            var item = new T();
                            for (int i = 1; i < columns.Count; i++) // Skip the first '_Key' column
                            {
                                columns[i].FromSqlValue(item, reader, converter);
                            }
                            result.Add(item);
                        }
                        return result;
                    }
                }
            }
        }
    }
}
