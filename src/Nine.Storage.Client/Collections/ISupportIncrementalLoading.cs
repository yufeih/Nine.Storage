namespace System.Collections.Specialized
{
    using System.Threading.Tasks;

    public interface ISupportIncrementalLoading
    {
        bool HasMoreItems { get; }

        Task<int> LoadMoreItemsAsync(int count);
    }
}
