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
    class KeyedTableEntity<T> : ITableEntity where T : IKeyed
    {
        private readonly KeyedTableEntityFormatter<T> formatter;

        public T Data { get; set; }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public string ETag { get; set; }
        public DateTimeOffset Timestamp { get; set; }

        public KeyedTableEntity(KeyedTableEntityFormatter<T> formatter)
        {
            this.formatter = formatter;
        }

        public void ReadEntity(IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {
            formatter.ReadEntity(Data, properties, operationContext);
        }

        public IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
        {
            return formatter.WriteEntity(Data, operationContext);
        }
    }
}
