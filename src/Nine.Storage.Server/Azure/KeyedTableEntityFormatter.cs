namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;
    using Nine.Formatting;

    class KeyedTableEntityFormatter<T>
    {
        private readonly TextConverter converter;
        private readonly PropertyInfo[] additionalProperties;

        public KeyedTableEntityFormatter(TextConverter converter)
        {
            this.converter = converter;
            this.additionalProperties = GetReflectedProperties(converter).ToArray();
        }

        public void ReadEntity(T Data, IDictionary<string, EntityProperty> properties, OperationContext operationContext)
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
                        }
                    }
                    else if (property.PropertyType == typeof(TimeSpan) && entityProperty.PropertyType == EdmType.Int64 && entityProperty.Int64Value.HasValue)
                    {
                        property.SetValue(Data, TimeSpan.FromTicks(entityProperty.Int64Value.Value));
                    }
                    else if (entityProperty.PropertyType == EdmType.String)
                    {
                        property.SetValue(Data, converter.FromText(property.PropertyType, entityProperty.StringValue));
                    }
                    properties.Remove(property.Name);
                }
            }

            TableEntity.ReadUserObject(Data, properties, operationContext);
        }

        public IDictionary<string, EntityProperty> WriteEntity(T Data, OperationContext operationContext)
        {
            var properties = TableEntity.WriteUserObject(Data, operationContext);

            foreach (var property in additionalProperties)
            {
                if (properties.ContainsKey(property.Name))
                {
                    continue;
                }

                if (property.PropertyType.IsEnum)
                {
                    properties.Add(property.Name, EntityProperty.GeneratePropertyForString(property.GetValue(Data).ToString()));
                }
                else if (property.PropertyType == typeof(TimeSpan))
                {
                    properties.Add(property.Name, EntityProperty.GeneratePropertyForLong(((TimeSpan)property.GetValue(Data)).Ticks));
                }
                else
                {
                    properties.Add(property.Name, EntityProperty.GeneratePropertyForString(converter.ToText(property.GetValue(Data))));
                }
            }

            return properties;
        }

        private IEnumerable<PropertyInfo> GetReflectedProperties(TextConverter converter)
        {
            foreach (var property in typeof(T).GetTypeInfo().DeclaredProperties)
            {
                if (property.GetMethod != null && property.GetMethod.IsPublic &&
                    property.SetMethod != null && property.SetMethod.IsPublic &&
                    property.GetIndexParameters().Length <= 0)
                {
                    if (property.PropertyType.IsEnum) yield return property;
                    if (property.PropertyType == typeof(TimeSpan)) yield return property;
                    if (converter != null && converter.CanConvert(property.PropertyType)) yield return property;
                }
            }
        }
    }
}
