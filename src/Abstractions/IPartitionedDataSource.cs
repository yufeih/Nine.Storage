namespace Nine.Storage.Batching
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public interface IPartitionedDataSource<T>
    {
        Task<IEnumerable<string>> GetPartitions();

        IAsyncEnumerator<T> GetValues(string partition);
    }
}
