namespace Xunit
{
    using System;
    using System.Collections.Generic;

    public interface ITestData<T>
    {
        IEnumerable<T> GetData();
    }

    public interface ITestFactory<T>
    {
        T Create();
    }

    public interface ITestFactoryData<T> : ITestData<ITestFactory<T>>
    { }

    public class TestDimension<TData, T> : TheoryData<T> where TData : ITestData<T>, new()
    {
        public TestDimension()
        {
            foreach (var data in new TData().GetData())
            {
                Add(data);
            }
        }
    }

    public class TestFactoryDimension<TData, T> : TestDimension<TData, ITestFactory<T>> where TData : ITestData<ITestFactory<T>>, new()
    { }

    public class TestFactory<T> : ITestFactory<T>
    {
        private readonly Func<T> factory;
        private readonly string name;

        public T Create() => factory();
        public override string ToString() => name;

        public TestFactory(Type type, Func<T> factory) : this(type.Name, factory) { }
        public TestFactory(string name, Func<T> factory) { this.name = name; this.factory = factory; }
    }
}
