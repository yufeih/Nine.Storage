namespace Nine.Storage.Collections
{
    using System.Threading.Tasks;

    public interface ISupportIncrementalLoading
    {
        bool HasMoreItems { get; }

        Task<int> LoadMoreItemsAsync(int count);
    }
}
