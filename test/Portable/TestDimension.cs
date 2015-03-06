namespace Xunit
{
    using System.Collections.Generic;

    public interface ITestData<T>
    {
        IEnumerable<T> GetData();
    }

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
}
