namespace Nine.Storage
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;
    using Nine.Formatting;

    /// <summary>
    /// A custom implementation of ITableEntity that support serializing Enum and TimeSpan properties of an arbitrary object.
    /// </summary>
    class KeyedTableEntity : ITableEntity
    {
        private TextConverter converter;

        public IKeyed Data { get; set; }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public string ETag { get; set; }
        public DateTimeOffset Timestamp { get; set; }

        public KeyedTableEntity(TextConverter converter)
        {
            if (converter != null) throw new NotImplementedException();
            this.converter = converter;
        }

        public void ReadEntity(IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {
            var additionalProperties = GetCachedReflectedProperties(Data.GetType());
            if (additionalProperties.Length > 0)
            {
                foreach (var property in additionalProperties)
                {
                    EntityProperty entityProperty;
                    if (properties.TryGetValue(property.Name, out entityProperty))
                    {
                        if (property.PropertyType.IsEnum)
                        {
                            if (entityProperty.PropertyType == EdmType.Int32 && entityProperty.Int32Value.HasValue)
                            {
                                property.SetValue(Data, entityProperty.Int32Value.Value);
                                properties.Remove(property.Name);
                            }
                            else if (entityProperty.PropertyType == EdmType.String)
                            {
                                object enumValue = null;
                                try
                                {
                                    enumValue = Enum.Parse(property.PropertyType, entityProperty.StringValue, true); 
                                }
                                catch (Exception e)
                                {
                                    Debug.WriteLine(e.ToString());
                                }
                                if (enumValue != null)
                                {
                                    property.SetValue(Data, enumValue);
                                }
                                properties.Remove(property.Name);
                            }
                        }
                        else if (property.PropertyType == typeof(TimeSpan) && entityProperty.PropertyType == EdmType.Int64 && entityProperty.Int64Value.HasValue)
                        {
                            property.SetValue(Data, TimeSpan.FromTicks(entityProperty.Int64Value.Value));
                            properties.Remove(property.Name);
                        }
                    }
                }
            }

            TableEntity.ReadUserObject(Data, properties, operationContext);
        }

        public IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
        {
            var properties = TableEntity.WriteUserObject(Data, operationContext);

            var additionalProperties = GetCachedReflectedProperties(Data.GetType());
            if (additionalProperties.Length > 0)
            {
                foreach (var property in additionalProperties)
                {
                    if (property.PropertyType.IsEnum)
                    {
                        properties.Add(property.Name, EntityProperty.GeneratePropertyForInt((int)property.GetValue(Data)));
                    }
                    else if (property.PropertyType == typeof(TimeSpan))
                    {
                        properties.Add(property.Name, EntityProperty.GeneratePropertyForLong(((TimeSpan)property.GetValue(Data)).Ticks));
                    }
                }
            }

            return properties;
        }

        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> reflectedProperties = new ConcurrentDictionary<Type, PropertyInfo[]>();

        private static PropertyInfo[] GetCachedReflectedProperties(Type type)
        {
            return reflectedProperties.GetOrAdd(type, x => GetReflectedProperties(x).ToArray());
        }

        private static IEnumerable<PropertyInfo> GetReflectedProperties(Type type)
        {
            foreach (var property in type.GetTypeInfo().DeclaredProperties)
            {
                if (property.GetMethod != null && property.GetMethod.IsPublic &&
                    property.SetMethod != null && property.SetMethod.IsPublic &&
                    property.GetIndexParameters().Length <= 0)
                {
                    if (property.PropertyType.IsEnum) yield return property;
                    if (property.PropertyType == typeof(TimeSpan)) yield return property;
                }
            }
        }
    }
}
