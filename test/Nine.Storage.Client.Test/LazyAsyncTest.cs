namespace Nine.Storage
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;
    
    public class LazyAsyncTest
    {
        [Fact]
        public async Task concurrent_lazy_requests()
        {
            for (var i = 0; i < 100; i++)
            {
                var n = 0;
                var lazy = new LazyAsync<int>(async () =>
                {
                    await Task.Delay(10);
                    return Interlocked.Increment(ref n);
                });

                var bag = new ConcurrentBag<Task<int>>();
                Parallel.For(0, 1000, j =>
                {
                    bag.Add(lazy.GetValueAsync());
                });

                var results = await Task.WhenAll(bag);

                for (int nn = 0; nn < 1000; nn++)
                {
                    Assert.Equal(1, results[nn]);
                }
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task retry_on_failure(bool retry)
        {
            var n = 0;
            var lazy = new LazyAsync<int>(() =>
            {
                n++;
                throw new NotImplementedException();
            }, retry);

            try { await lazy.GetValueAsync(); } catch (NotImplementedException) { }
            try { await lazy.GetValueAsync(); } catch (NotImplementedException) { }

            Assert.Equal(retry ? 2 : 1, n);
        }
    }
}
