namespace Nine.Storage
{
    using System;
    using ProtoBuf;

    [ProtoContract]
    public class BasicTypes : BasicTypesBase { }

    [ProtoContract]
    public class BasicTypesBase
    {
        [ProtoMember(1)]
        public short Short = short.MinValue;
        [ProtoMember(2)]
        public ushort Ushort = ushort.MaxValue;
        [ProtoMember(3)]
        public int Int = int.MaxValue;
        [ProtoMember(4)]
        public long Long = long.MinValue;
        //[ProtoMember(5)] NotSupported by bson
        //public ulong Ulong = ulong.MaxValue;
        [ProtoMember(6)]
        public string String = "THis afowejfksflskdjflskdf中饿鬼送我饥饿感ijifwjeifaaas";
        [ProtoMember(7)]
        public StringComparison Enum = StringComparison.OrdinalIgnoreCase;
        //[ProtoMember(8)] Not supported by protobuf
        //public StringComparison EnumOverflow = (StringComparison)int.MaxValue;
        [ProtoMember(9)]
        public DateTime DateTime = DateTime.UtcNow;
        [ProtoMember(10)]
        public DateTime MinDateTime = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
        [ProtoMember(11)]
        public DateTime MaxDateTime = DateTime.SpecifyKind(DateTime.MaxValue.AddDays(-1), DateTimeKind.Utc);
        //[ProtoMember(12)]
        //public TimeSpan TimeSpan = TimeSpan.MaxValue;
        [ProtoMember(13)]
        public int? Nullable;
        [ProtoMember(14)]
        public int? NullableWithValue = int.MaxValue;
        //[ProtoMember(15)] Not supported by protobuf
        //public Type Type = typeof(BasicTypes);
        //[ProtoMember(16)]
        //public Type NullType;

#pragma warning disable CS0414
        private string notSerialized = "notSerialized";
#pragma warning restore
    }
}
