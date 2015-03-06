namespace System.Collections.Generic
{
    using System.ComponentModel;
    using System.Linq;
    using System.Threading.Tasks;

    public interface IAsyncEnumerator<T>
    {
        bool HasMore { get; }
        Task<IEnumerable<T>> LoadMoreAsync();
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct AsyncEnumerationResult<T>
    {
        public bool HasMore;
        public IEnumerable<T> Items;
    }

    public static class AsyncEnumerator
    {
        class EmptyAsyncEnumerator<T> : IAsyncEnumerator<T>
        {
            public bool HasMore { get { return false; } }

            public Task<IEnumerable<T>> LoadMoreAsync()
            {
                return Task.FromResult(Enumerable.Empty<T>());
            }
        }

        class DelegatingAsyncEnumerator<T> : IAsyncEnumerator<T>
        {
            public bool HasMore { get; set; }
            public Func<Task<AsyncEnumerationResult<T>>> Next;

            public async Task<IEnumerable<T>> LoadMoreAsync()
            {
                var result = await Next().ConfigureAwait(false);
                HasMore = result.HasMore;
                return result.Items;
            }
        }

        public static IAsyncEnumerator<T> Empty<T>()
        {
            return new EmptyAsyncEnumerator<T>();
        }

        public static IAsyncEnumerator<T> Create<T>(Func<Task<AsyncEnumerationResult<T>>> func)
        {
            return new DelegatingAsyncEnumerator<T> { Next = func, HasMore = true };
        }

        public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerator<T> enumerator)
        {
            var result = new List<T>();
            while (enumerator.HasMore)
            {
                result.AddRange(await enumerator.LoadMoreAsync().ConfigureAwait(false));
            }
            return result;
        }
    }
}
