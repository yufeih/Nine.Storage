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
            return $"{ value.X },{ value.Y }";
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
            Storage.Add(() => new TableStorage<ClassWithCustomMembers>(Connection.Current.AzureStorage));
            Storage.Add(() => new BatchedTableStorage<ClassWithCustomMembers>(Connection.Current.AzureStorage));
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
