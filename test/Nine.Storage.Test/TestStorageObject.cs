namespace Nine.Storage
{
    using System;
    using System.Runtime.Serialization;

    [DataContract]
    public class TestStorageObject : TestStorageObjectBase
    {
        public TestStorageObject()
        {
            Time3 = DateTime.UtcNow;
        }

        public TestStorageObject(object id)
        {
            Time3 = DateTime.UtcNow;
            Id = id.ToString();
        }
    }

    [DataContract]
    public class TestStorageObjectBase : IKeyed
    {
        [DataMember(Order = 1)]
        public string Id { get; set; }
        [DataMember(Order = 2)]
        public string Name { get; set; }
        [DataMember(Order = 3)]
        public string ApplicationName { get; set; }
        [DataMember(Order = 4)]
        public DateTime Time { get; set; } = DateTime.UtcNow;
        [DataMember(Order = 5)]
        public DateTime? NullableTime { get; set; }
        [DataMember(Order = 6)]
        public DateTime? NullableTime2 { get; set; }
        [DataMember(Order = 7)]
        public StringComparison Enum { get; set; }
        //[DataMember(Order = 8)]
        //public TimeSpan TimeSpan { get; set; }
        [DataMember(Order = 9)]
        public DateTime Time3 { get; set; }

        public string GetKey() => Id;
        public override string ToString() => Id;
    }
}
