namespace Nine.Storage
{
    using System;
    using System.IO;
    using System.Diagnostics;
    using System.Globalization;
    using System.Reflection;
    using System.Text;

    [DebuggerStepThrough]
    public static class StorageKey
    {
        public static readonly string Separator = "-";

        /// <summary>
        /// Concatenate each component by "-", and ensures lexicographical ordering of basic types.
        /// </summary>
        public static string Get<T>(T obj)
        {
            var sb = StringBuilderCache.Acquire();

            Append(sb, obj, true);

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        /// <summary>
        /// Concatenate each component by "-", and ensures lexicographical ordering of basic types.
        /// </summary>
        public static string Get<T1, T2>(T1 obj1, T2 obj2)
        {
            var sb = StringBuilderCache.Acquire();

            Append(sb, obj1, false);
            sb.Append(Separator);
            Append(sb, obj2, true);

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        /// <summary>
        /// Concatenate each component by "-", and ensures lexicographical ordering of basic types.
        /// </summary>
        public static string Get<T1, T2, T3>(T1 obj1, T2 obj2, T3 obj3)
        {
            var sb = StringBuilderCache.Acquire();

            Append(sb, obj1, false);
            sb.Append(Separator);
            Append(sb, obj2, false);
            sb.Append(Separator);
            Append(sb, obj3, true);

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        /// <summary>
        /// Concatenate each component by "-", and ensures lexicographical ordering of basic types.
        /// </summary>
        public static string Get(params object[] values)
        {
            if (values == null || values.Length < 1) return null;

            var sb = StringBuilderCache.Acquire();

            for (int i = 0; i < values.Length; i++)
            {
                var value = values[i];
                var isLast = i == values.Length - 1;

                Append(sb, value, isLast);

                if (!isLast) sb.Append(Separator);
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        private static void Append(StringBuilder sb, object value, bool isLast)
        {
            unchecked
            {
                if (value == null) throw new ArgumentNullException();

                var type = value.GetType();

                if (type == typeof(string))
                {
                    var component = value.ToString();

                    // Skip the last key component
                    if (!isLast)
                    {
                        foreach (var c in component)
                        {
                            var valid = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
                            if (!valid) throw new ArgumentException("string can only contain numbers and characters.");
                        }
                    }

                    sb.Append(component);
                }
                else if (type == typeof(DateTime))
                {
                    AppendDateTime(sb, (DateTime)value);
                }
                else if (type == typeof(ulong))
                {
                    sb.Append(((ulong)value).ToString("D20"));
                }
                else if (type == typeof(long))
                {
                    sb.Append(((ulong)((long)value + long.MaxValue + 1)).ToString("D20"));
                }
                else if (type == typeof(uint))
                {
                    sb.Append(((uint)value).ToString("D10"));
                }
                else if (type == typeof(int))
                {
                    sb.Append(((uint)((int)value + int.MaxValue + 1)).ToString("D10"));
                }
                else if (type == typeof(ushort))
                {
                    sb.Append(((ushort)value).ToString("D5"));
                }
                else if (type == typeof(short))
                {
                    sb.Append(((ushort)((short)value + short.MaxValue + 1)).ToString("D5"));
                }
                else if (type == typeof(byte))
                {
                    sb.Append(((byte)value).ToString("D3"));
                }
                else if (type == typeof(sbyte))
                {
                    sb.Append(((byte)((sbyte)value + sbyte.MaxValue + 1)).ToString("D3"));
                }
                else if (type == typeof(TimeSpan))
                {
                    sb.Append(((ulong)(((TimeSpan)value).Ticks + long.MaxValue + 1)).ToString("D20"));
                }
                else if (type == typeof(bool))
                {
                    sb.Append((bool)value ? "1" : "0");
                }
                else if (type.GetTypeInfo().IsEnum)
                {
                    sb.Append(value.ToString());
                }
                else
                {
                    // There is no way for us to determine if a type is enum in PCL !!!
                    throw new NotSupportedException(string.Format("{0} is not supported as a key value", type));
                }
            }
        }

        /// <summary>
        /// Converts the time to a string for key value pair storage system whose
        /// keys are sorted using alphabetic order.
        /// </summary>
        private static void AppendDateTime(StringBuilder sb, DateTime time)
        {
            // Make sure everything is measured in UTC.
            if (time.Kind == DateTimeKind.Local)
            {
                time = time.ToUniversalTime();
            }

            // A faster method to turn DateTime into yyyyMMddHHmmssfffffff format.

            var x = time.Year;
            sb.Append(x / 1000);
            x %= 1000;
            sb.Append(x / 100);
            x %= 100;
            sb.Append(x / 10);
            sb.Append(x % 10);

            x = time.Month;
            sb.Append(x / 10);
            sb.Append(x % 10);

            x = time.Day;
            sb.Append(x / 10);
            sb.Append(x % 10);

            x = time.Hour;
            sb.Append(x / 10);
            sb.Append(x % 10);

            x = time.Minute;
            sb.Append(x / 10);
            sb.Append(x % 10);

            x = time.Second;
            sb.Append(x / 10);
            sb.Append(x % 10);

            var f = time.Ticks % 10000000L;
            var d = 1000000L;

            for (int i = 0; i < 7; i++)
            {
                sb.Append(f / d);
                f %= d;
                d /= 10;
            }
        }

        /// <summary>
        /// Parses the string representation of the key and cast it to the specified type.
        /// </summary>
        public static T Parse<T>(string value)
        {
            unchecked
            {
                var type = typeof(T);
                if (type == typeof(string))
                {
                    if (value.Contains(Separator)) throw new InvalidOperationException("value cannot contain -");
                    return (T)(object)value;
                }
                else if (type == typeof(DateTime))
                {
                    return (T)(object)DateTime.ParseExact(value, "yyyyMMddHHmmssfffffff", CultureInfo.InvariantCulture);
                }
                else if (type == typeof(ulong))
                {
                    return (T)(object)ulong.Parse(value);
                }
                else if (type == typeof(long))
                {
                    return (T)(object)(long)(ulong.Parse(value) - long.MaxValue - 1);
                }
                else if (type == typeof(uint))
                {
                    return (T)(object)uint.Parse(value);
                }
                else if (type == typeof(int))
                {
                    return (T)(object)(int)(uint.Parse(value) - int.MaxValue - 1);
                }
                else if (type == typeof(ushort))
                {
                    return (T)(object)ushort.Parse(value);
                }
                else if (type == typeof(short))
                {
                    return (T)(object)(short)(ushort.Parse(value) - short.MaxValue - 1);
                }
                else if (type == typeof(byte))
                {
                    return (T)(object)byte.Parse(value);
                }
                else if (type == typeof(sbyte))
                {
                    return (T)(object)(byte)(byte.Parse(value) - sbyte.MaxValue - 1);
                }
                else if (type == typeof(TimeSpan))
                {
                    return (T)(object)TimeSpan.FromTicks((long)(ulong.Parse(value) - long.MaxValue - 1));
                }
                else if (type == typeof(bool))
                {
                    int intVal;
                    if (int.TryParse(value, out intVal)) return (T)(object)(intVal != 0);
                    return (T)(object)bool.Parse(value);
                }
                else
                {
                    throw new NotSupportedException(string.Format("{0} is not supported as a key value", type));
                }
            }
        }

        private static readonly char[] componentSplitChars = new[] { '-' };

        public static Tuple<T1, T2> Parse<T1, T2>(string value)
        {
            var components = value.Split(componentSplitChars);
            return Tuple.Create(Parse<T1>(components[0]), Parse<T2>(components[1]));
        }

        public static Tuple<T1, T2, T3> Parse<T1, T2, T3>(string value)
        {
            var components = value.Split(componentSplitChars);
            return Tuple.Create(Parse<T1>(components[0]), Parse<T2>(components[1]), Parse<T3>(components[2]));
        }

        public static Tuple<T1, T2, T3, T4> Parse<T1, T2, T3, T4>(string value)
        {
            var components = value.Split(componentSplitChars);
            return Tuple.Create(Parse<T1>(components[0]), Parse<T2>(components[1]), Parse<T3>(components[2]), Parse<T4>(components[3]));
        }

        /// <summary>
        /// Increment the input value in lexicographical order. E.g. abc will be turned into abd.
        /// </summary>
        public static string Increment(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            var sb = StringBuilderCache.Acquire();

            var last = key.Length - 1;

            for (var i = 0; i < last; i++)
                sb.Append(key[i]);

            checked
            {
                sb.Append((char)(key[last] + 1));
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        /// <summary>
        /// Determines if the incrementedKey is incremented from key.
        /// </summary>
        public static bool IsIncrement(string key, string incrementedKey)
        {
            if (string.IsNullOrEmpty(key) && string.IsNullOrEmpty(incrementedKey)) return true;
            if (key == null || incrementedKey == null) return false;
            if (key.Length != incrementedKey.Length) return false;

            checked
            {
                return incrementedKey[incrementedKey.Length - 1] == key[key.Length - 1] + 1;
            }
        }

        /// <summary>
        /// Decrement the input value in lexicographical order. E.g. abc will be turned into abb.
        /// </summary>
        public static string Decrement(string key)
        {
            if (key == null) throw new ArgumentNullException("key");

            var sb = StringBuilderCache.Acquire();

            var last = key.Length - 1;

            for (var i = 0; i < last; i++)
                sb.Append(key[i]);

            checked
            {
                sb.Append((char)(key[last] - 1));
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }
    }
}