namespace Nine.Storage
{
    using System;
    using System.Threading.Tasks;
    using Nine.Formatting;
    using Xunit;

    public struct Vector2 { public double X, Y; }

    public class Vector2Converter : ITextConverter<Vector2>
    {
        public Vector2 FromText(string text)
        {
            var parts = text.Split(',');
            return new Vector2 { X = double.Parse(parts[0]), Y = double.Parse(parts[1]) };
        }

        public string ToText(Vector2 value)
        {
            // In some cases, Double values formatted with the "R" standard numeric format string
            // do not successfully round-trip if compiled using the /platform:x64 or
            // /platform:anycpu switches and run on 64-bit systems.
            // To work around this problem, you can format Double values by using the 
            // "G17" standard numeric format string
            // https://msdn.microsoft.com/en-us/library/kfsatb94(v=vs.110).aspx
            return $"{ value.X.ToString("G17") },{ value.Y.ToString("G17") }";
        }
    }

    public class ClassWithCustomMembers : IKeyed
    {
        public string Id { get; set; }
        public Vector2 Position { get; set; }

        public string GetKey() => Id;
    }

    public class TextConverterTest
    {
        private static readonly TextConverter converter = new TextConverter(new Vector2Converter());
        private static Random random = new Random();

        public static TheoryData<Func<IStorage<ClassWithCustomMembers>>> Storage = new TheoryData<Func<IStorage<ClassWithCustomMembers>>>();

        static TextConverterTest()
        {
            if (!string.IsNullOrEmpty(Connection.Current.AzureStorage))
            {
                Storage.Add(() => new TableStorage<ClassWithCustomMembers>(Connection.Current.AzureStorage, null, false, converter));
                Storage.Add(() => new BatchedTableStorage<ClassWithCustomMembers>(Connection.Current.AzureStorage, null, 2, 1, converter));
            }
            Storage.Add(() => new MemoryStorage<ClassWithCustomMembers>());
        }

        [Theory, MemberData("Storage")]
        public async Task it_should_respect_text_converter(Func<IStorage<ClassWithCustomMembers>> factory)
        {
            var store = factory();
            var vector = new Vector2 { X = random.NextDouble(), Y = random.NextDouble() };
            var id = Guid.NewGuid().ToString();
            await store.Put(new ClassWithCustomMembers { Id = id, Position = vector });
            var retrieved = await store.Get(id);

            Assert.Equal(vector.X, retrieved.Position.X);
            Assert.Equal(vector.Y, retrieved.Position.Y);
        }
    }
}
