namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Nine.Formatting;

    class SqlColumn
    {
        public const int MaxKeySizeInBytes = 1024;
        public const string KeyColumnName = "_Key";

        private static readonly Encoding utf8 = new UTF8Encoding(false, false);
        private static readonly Dictionary<Type, SqlDbType> typeToDbType = new Dictionary<Type, SqlDbType>
        {
            { typeof(bool), SqlDbType.Bit },
            { typeof(int), SqlDbType.Int },
            { typeof(uint), SqlDbType.Int },
            { typeof(short), SqlDbType.SmallInt },
            { typeof(ushort), SqlDbType.SmallInt },
            { typeof(long), SqlDbType.BigInt },
            { typeof(ulong), SqlDbType.BigInt },
            { typeof(DateTime), SqlDbType.DateTime2 },
            { typeof(TimeSpan), SqlDbType.Time },
            { typeof(byte[]), SqlDbType.VarBinary },
        };

        private Func<object, object> getter;
        private Action<object, object> setter;
        private bool isNullable;
        private int capacity;
        private Type type;

        public string Name { get; private set; }

        private SqlColumn(Type type, bool? isNullable = null)
        {
            if (isNullable.HasValue)
            {
                this.type = type;
                this.isNullable = isNullable.Value;
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                this.type = Nullable.GetUnderlyingType(type);
                this.isNullable = true;
            }
            else
            {
                this.type = type;
                this.isNullable = !type.IsValueType && !type.IsEnum;
            }
        }

        public static IEnumerable<SqlColumn> FromType(Type type)
        {
            var properties =
                from p in type.GetTypeInfo().DeclaredProperties
                where p.GetMethod != null && p.GetMethod.IsPublic &&
                      p.SetMethod != null && p.SetMethod.IsPublic &&
                      p.GetIndexParameters().Length <= 0
                select new SqlColumn(p.PropertyType)
                {
                    Name = p.Name,
                    getter = (target) => p.GetMethod.Invoke(target, null),
                    setter = (target, value) => p.SetMethod.Invoke(target, new[] { value }),
                };

            var fields =
                from f in type.GetTypeInfo().DeclaredFields
                where f.IsPublic
                select new SqlColumn(f.FieldType)
                {
                    Name = f.Name,
                    getter = (target) => f.GetValue(target),
                    setter = (target, value) => f.SetValue(target, value),
                };

            var primaryKey = new[]
            {
                new SqlColumn(typeof(byte[]), false)
                {
                    Name = KeyColumnName, capacity = MaxKeySizeInBytes,
                    getter = (target) => utf8.GetBytes(((IKeyed)target).GetKey()),
                    setter = (target, value) => { },
                }
            };

            return primaryKey.Concat(properties.Concat(fields).OrderBy(x => x.Name));
        }

        public string ToDbTypeText()
        {
            var result = ToDbTypeTextCore();
            return isNullable ? result + " null" : result;
        }

        private string ToDbTypeTextCore()
        {
            SqlDbType result;

            var size = capacity <= 0 ? "max" : capacity.ToString();

            if (type == typeof(byte[])) return $"varbinary({ size })";
            if (typeToDbType.TryGetValue(type, out result)) return result.ToString().ToLowerInvariant();
            if (type.IsEnum) return "int";

            return $"nvarchar({ size })";
        }

        public object ToSqlValue(object value, TextConverter converter)
        {
            value = getter(value);

            if (value == null) return DBNull.Value;

            if (type.IsEnum) return (int)value;
            if (type.IsPrimitive) return value;
            if (type == typeof(string)) return value;
            if (converter != null && converter.CanConvert(type)) return converter.ToText(value);

            return value;
        }

        public void FromSqlValue(object result, SqlDataReader reader, TextConverter converter)
        {
            var value = reader[Name];
            if (value is DBNull) value = null;

            if (converter != null && !type.IsEnum && !type.IsPrimitive && 
                type != typeof(string) && converter.CanConvert(type))
            {
                value = converter.FromText(type, (string)value);
            }

            setter(result, value);
        }

        public static byte[] ToBytes(string key)
        {
            return utf8.GetBytes(key);
        }
    }
}
