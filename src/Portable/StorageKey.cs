namespace Nine.Storage
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Reflection;
    using System.Security;
    using System.Text;

    [DebuggerStepThrough]
    public static class StorageKey
    {
        public static readonly string Separator = "-";

        /// <summary>
        /// Concatenate each component by "-", and ensures lexicographical ordering of basic types.
        /// </summary>
        [SecuritySafeCritical]
        public static string Get(params object[] values)
        {
            if (values == null || values.Length < 1) return null;

            unchecked
            {
                var result = new StringBuilder(128);

                for (int i = 0; i < values.Length; i++)
                {
                    var value = values[i];
                    if (value == null) continue;

                    var type = value.GetType();
                    var component = (string)null;

                    if (type == typeof(string))
                    {
                        component = value.ToString();

                        // Skip the last key component
                        if (i != values.Length - 1)
                        {
                            foreach (var c in component)
                            {
                                var valid = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
                                if (!valid) throw new ArgumentException("string can only contain numbers and characters.");
                            }
                        }
                    }
                    else if (type == typeof(DateTime))
                    {
                        component = ToStorageString((DateTime)value);
                    }
                    else if (type == typeof(ulong))
                    {
                        component = ((ulong)value).ToString("D20");
                    }
                    else if (type == typeof(long))
                    {
                        component = ((ulong)((long)value + long.MaxValue + 1)).ToString("D20");
                    }
                    else if (type == typeof(uint))
                    {
                        component = ((uint)value).ToString("D10");
                    }
                    else if (type == typeof(int))
                    {
                        component = ((uint)((int)value + int.MaxValue + 1)).ToString("D10");
                    }
                    else if (type == typeof(ushort))
                    {
                        component = ((ushort)value).ToString("D5");
                    }
                    else if (type == typeof(short))
                    {
                        component = ((ushort)((short)value + short.MaxValue + 1)).ToString("D5");
                    }
                    else if (type == typeof(byte))
                    {
                        component = ((byte)value).ToString("D3");
                    }
                    else if (type == typeof(sbyte))
                    {
                        component = ((byte)((sbyte)value + sbyte.MaxValue + 1)).ToString("D3");
                    }
                    else if (type == typeof(TimeSpan))
                    {
                        component = ((ulong)(((TimeSpan)value).Ticks + long.MaxValue + 1)).ToString("D20");
                    }
                    else if (type.GetTypeInfo().IsEnum)
                    {
                        component = value.ToString();
                    }
                    else
                    {
                        // There is no way for us to determine if a type is enum in PCL !!!
                        throw new NotSupportedException(string.Format("{0} is not supported as a key value", type));
                    }

                    result.Append(component);

                    if (i < values.Length - 1) result.Append(Separator);
                }

                return result.ToString();
            }
        }

        /// <summary>
        /// Converts the time to a string for key value pair storage system whose
        /// keys are sorted using alphabetic order.
        /// </summary>
        private static string ToStorageString(DateTime time)
        {
            // Make sure everything is measured in UTC.
            if (time.Kind == DateTimeKind.Local)
            {
                time = time.ToUniversalTime();
            }

            // A faster method to turn DateTime into yyyyMMddHHmmssfffffff format.
            var result = new StringBuilder(4 + 2 + 2 + 2 + 2 + 2 + 7);

            var x = time.Year;
            result.Append(x / 1000);
            x %= 1000;
            result.Append(x / 100);
            x %= 100;
            result.Append(x / 10);
            result.Append(x % 10);

            x = time.Month;
            result.Append(x / 10);
            result.Append(x % 10);

            x = time.Day;
            result.Append(x / 10);
            result.Append(x % 10);

            x = time.Hour;
            result.Append(x / 10);
            result.Append(x % 10);

            x = time.Minute;
            result.Append(x / 10);
            result.Append(x % 10);

            x = time.Second;
            result.Append(x / 10);
            result.Append(x % 10);

            var f = time.Ticks % 10000000L;
            var d = 1000000L;

            for (int i = 0; i < 7; i++)
            {
                result.Append(f / d);
                f %= d;
                d /= 10;
            }

            return result.ToString();
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

            var bytes = key.ToCharArray();
            var last = bytes.Length - 1;

            checked
            {
                bytes[last] = (char)(bytes[last] + 1);
            }

            return new string(bytes);
        }

        /// <summary>
        /// Determines if the incrementedKey is incremented from key.
        /// </summary>
        public static bool IsIncrement(string key, string incrementedKey)
        {
            if (string.IsNullOrEmpty(key) && string.IsNullOrEmpty(incrementedKey)) return true;
            if (key == null || incrementedKey == null) return false;

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

            var bytes = key.ToCharArray();
            var last = bytes.Length - 1;

            checked
            {
                bytes[last] = (char)(bytes[last] - 1);
            }

            return new string(bytes);
        }
    }
}