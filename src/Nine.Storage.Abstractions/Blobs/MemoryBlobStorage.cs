namespace Nine.Storage.Blobs
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public class MemoryBlobStorage : IBlobStorage
    {
        private readonly ConcurrentDictionary<string, MemoryStream> _store = new ConcurrentDictionary<string, MemoryStream>(StringComparer.OrdinalIgnoreCase);

        public virtual Task<bool> Exists(string key)
        {
            if (string.IsNullOrEmpty(key)) return Task.FromResult(false);
            return Task.FromResult(_store.ContainsKey(key));
        }

        public virtual Task<string> GetUri(string key) => CommonTasks.NullString;

        public virtual Task<Stream> Get(string key, IProgress<ProgressInBytes> progress = null, CancellationToken cancellation = default(CancellationToken))
        {
            MemoryStream result;
            if (string.IsNullOrEmpty(key)) return Task.FromResult<Stream>(null);
            if (_store.TryGetValue(key, out result) && result != null)
            {
                result.Seek(0, SeekOrigin.Begin);
                return Task.FromResult<Stream>(result);
            }
            return CommonTasks.Null<Stream>();
        }

        public virtual Task<string> Put(string key, Stream stream, IProgress<ProgressInBytes> progress = null, CancellationToken cancellation = default(CancellationToken))
        {
            var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Seek(0, SeekOrigin.Begin);
            _store.GetOrAdd(key, ms);
            return Task.FromResult(key);
        }

        public virtual Task Delete(string key)
        {
            MemoryStream removed;
            _store.TryRemove(key, out removed);
            return CommonTasks.Completed;
        }
    }
}
