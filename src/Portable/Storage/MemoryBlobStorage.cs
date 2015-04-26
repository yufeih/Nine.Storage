namespace Nine.Storage
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public class MemoryBlobStorage : IBlobStorage
    {
        private readonly ConcurrentDictionary<string, byte[]> store = new ConcurrentDictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        public virtual Task<bool> Exists(string key)
        {
            if (string.IsNullOrEmpty(key)) return Task.FromResult(false);
            return Task.FromResult(store.ContainsKey(key));
        }

        public virtual Task<string> GetUri(string key)
        {
            return Task.FromResult<string>(null);
        }

        public virtual Task<Stream> Get(string key, IProgress<ProgressInBytes> progress = null, CancellationToken cancellation = default(CancellationToken))
        {
            byte[] bytes;
            if (string.IsNullOrEmpty(key)) return Task.FromResult<Stream>(null);
            return Task.FromResult<Stream>(store.TryGetValue(key, out bytes) ? new MemoryStream(bytes) : null);
        }

        public virtual Task<string> Put(string key, Stream stream, IProgress<ProgressInBytes> progress = null, CancellationToken cancellation = default(CancellationToken))
        {
            var bytes = new byte[stream.Length];
            stream.Read(bytes, 0, (int)stream.Length);
            store.GetOrAdd(key, bytes);
            return Task.FromResult(key);
        }
    }
}
