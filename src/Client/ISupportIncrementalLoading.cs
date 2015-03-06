namespace Nine.Storage
{
    using System.Threading.Tasks;

    public interface ISupportIncrementalLoading
    {
        bool HasMoreItems { get; }

        Task<int> LoadMoreItemsAsync(int count);
    }
}
