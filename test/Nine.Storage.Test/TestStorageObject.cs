namespace Nine.Storage
{
    using System;
    using ProtoBuf;

    [ProtoContract]
    public class TestStorageObject : IKeyed, ITimestamped
    {
        [ProtoMember(1)]
        public string Id { get; set; }
        [ProtoMember(2)]
        public string Name { get; set; }
        [ProtoMember(3)]
        public string ApplicationName { get; set; }
        [ProtoMember(4)]
        public DateTime Time { get; set; } = DateTime.UtcNow;
        [ProtoMember(5)]
        public DateTime? NullableTime { get; set; }
        [ProtoMember(6)]
        public DateTime? NullableTime2 { get; set; }
        [ProtoMember(7)]
        public StringComparison Enum { get; set; }
        [ProtoMember(8)]
        public TimeSpan TimeSpan { get; set; }
        [ProtoMember(9)]
        public DateTime Time3 { get; set; }

        public TestStorageObject()
        {
            Time3 = DateTime.UtcNow;
        }

        public TestStorageObject(object id)
        {
            Time3 = DateTime.UtcNow;
            Id = id.ToString();
        }

        public string GetKey()
        {
            return Id;
        }

        public override string ToString()
        {
            return Id;
        }
    }
}
