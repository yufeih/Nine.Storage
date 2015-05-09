namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public class StorageMigration<T> : IStorage<T> where T : class, IKeyed, new()
    {
        private readonly bool deleteOldItems;
        private readonly IStorage<T> previous;
        private readonly IStorage<T> current;
        private readonly IPartitionedDataSource<T> previousSource;

        class ProgressReporter
        {
            public int Count;
            public IProgress<int> Progress;

            public void Increment(int count)
            {
                if (Progress != null && count > 0)
                {
                    Progress.Report(Interlocked.Add(ref Count, count));
                }
            }
        }

        public StorageMigration(IStorage<T> current, IStorage<T> previous, IPartitionedDataSource<T> previousSource = null, bool deleteOldItems = false)
        {
            if (previous == null && previousSource == null) throw new ArgumentNullException("previous");
            if (current == null) throw new ArgumentNullException("current");

            this.previous = previous;
            this.current = current;
            this.deleteOldItems = deleteOldItems;
            this.previousSource = previous as IPartitionedDataSource<T> ?? previousSource;
        }

        public async Task MigrateAsync(int? batchSize = 1000, IProgress<int> progress = null)
        {
            var reporter = new ProgressReporter { Progress = progress };
            if (previousSource == null)
            {
                await MigrateAsync(previous.All(batchSize), reporter);
            }
            else
            {
                var partitions = await previousSource.GetPartitions().ConfigureAwait(false);
                var tasks = partitions.Select(p => MigrateAsync(previousSource.GetValues(p), reporter));
                await Task.WhenAll(tasks);
            }
        }

        private async Task MigrateAsync(IAsyncEnumerator<T> enumerator, ProgressReporter progress)
        {
            while (enumerator.HasMore)
            {
                var items = await enumerator.LoadMoreAsync().ConfigureAwait(false);
                if (!items.Any()) return;

                var bulk = current as IBulkStorage<T>;
                if (bulk != null && !deleteOldItems)
                {
                    await bulk.Add(items);
                }
                else
                {
                    await Task.WhenAll(from x in items select MigrateOneAsync(x)).ConfigureAwait(false);
                }
                progress.Increment(items.Count());
            }
        }

        private async Task MigrateOneAsync(T item)
        {
            var key = item.GetKey();
            await current.Add(item).ConfigureAwait(false);
            if (deleteOldItems)
            {
                await previous.Delete(key).ConfigureAwait(false);
            }
        }

        public async Task<T> Get(string key)
        {
            var result = await current.Get(key).ConfigureAwait(false);
            if (result == null)
            {
                result = await previous.Get(key).ConfigureAwait(false);
                if (result != null)
                {
                    await MigrateOneAsync(result);
                }
            }
            return result;
        }

        public async Task<IEnumerable<T>> Range(string minKey, string maxKey, int? count = null)
        {
            var news = current.Range(minKey, maxKey, count);
            var olds = previous.Range(minKey, maxKey, count);

            await Task.WhenAll(news, olds).ConfigureAwait(false);

            if (!olds.Result.Any()) return news.Result;

            var merge = new Dictionary<string, T>(news.Result.Count() + olds.Result.Count());

            foreach (var item in news.Result)
            {
                merge.Add(item.GetKey(), item);
            }

            foreach (var item in olds.Result)
            {
                var key = item.GetKey();
                if (!merge.ContainsKey(key))
                {
                    merge.Add(key, item);
                    await MigrateOneAsync(item);
                }
            }

            var result = merge.OrderBy(x => x.Key).Select(x => x.Value);
            if (count.HasValue) result = result.Take(count.Value);

            return result.ToArray();
        }

        public Task<bool> Add(T value)
        {
            return current.Add(value);
        }

        public Task Put(T value)
        {
            return current.Put(value);
        }

        public async Task<bool> Delete(string key)
        {
            return (await Task.WhenAll(current.Delete(key), previous.Delete(key)).ConfigureAwait(false)).All(x => x);
        }
    }
}