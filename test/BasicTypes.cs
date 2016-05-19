namespace Nine.Storage
{
    using System;
    using System.Runtime.Serialization;

    [DataContract]
    public class BasicTypes : BasicTypesBase { }

    [DataContract]
    public class BasicTypesBase
    {
        [DataMember(Order = 1)]
        public short Short = short.MinValue;
        [DataMember(Order = 2)]
        public ushort Ushort = ushort.MaxValue;
        [DataMember(Order = 3)]
        public int Int = int.MaxValue;
        [DataMember(Order = 4)]
        public long Long = long.MinValue;
        //[DataMember(Order = 5)] NotSupported by bson
        //public ulong Ulong = ulong.MaxValue;
        [DataMember(Order = 6)]
        public string String = "THis afowejfksflskdjflskdf中饿鬼送我饥饿感ijifwjeifaaas";
        [DataMember(Order = 7)]
        public StringComparison Enum = StringComparison.OrdinalIgnoreCase;
        //[DataMember(Order = 8)] Not supported by protobuf
        //public StringComparison EnumOverflow = (StringComparison)int.MaxValue;
        [DataMember(Order = 9)]
        public DateTime DateTime = DateTime.UtcNow;
        [DataMember(Order = 10)]
        public DateTime MinDateTime = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
        [DataMember(Order = 11)]
        public DateTime MaxDateTime = DateTime.SpecifyKind(DateTime.MaxValue.AddDays(-1), DateTimeKind.Utc);
        //[DataMember(Order = 12)]
        //public TimeSpan TimeSpan = TimeSpan.MaxValue;
        [DataMember(Order = 13)]
        public int? Nullable;
        [DataMember(Order = 14)]
        public int? NullableWithValue = int.MaxValue;
        //[DataMember(Order = 15)] Not supported by protobuf
        //public Type Type = typeof(BasicTypes);
        //[DataMember(Order = 16)]
        //public Type NullType;

#pragma warning disable CS0414
        private string notSerialized = "notSerialized";
#pragma warning restore
    }
}
