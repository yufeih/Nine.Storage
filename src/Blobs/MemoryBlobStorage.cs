namespace Nine.Storage.Blobs
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public class MemoryBlobStorage : IBlobStorage
    {
        private readonly ConcurrentDictionary<string, byte[]> _store = new ConcurrentDictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        public virtual Task<bool> Exists(string key)
        {
            if (string.IsNullOrEmpty(key)) return Task.FromResult(false);
            return Task.FromResult(_store.ContainsKey(key));
        }

        public virtual string GetUri(string key) => null;

        public virtual Task<Stream> Get(string key, IProgress<ProgressInBytes> progress = null, CancellationToken cancellation = default(CancellationToken))
        {
            byte[] result;
            if (string.IsNullOrEmpty(key)) return Task.FromResult<Stream>(null);
            if (_store.TryGetValue(key, out result) && result != null)
            {
                return Task.FromResult<Stream>(new MemoryStream(result, writable: false));
            }
            return Tasks.Null<Stream>();
        }

        public virtual Task<string> Put(string key, Stream stream, IProgress<ProgressInBytes> progress = null, CancellationToken cancellation = default(CancellationToken))
        {
            long length;

            try
            {
                length = stream.Length;
            }
            catch (NotSupportedException)
            {
                var ms = new MemoryStream();
                stream.CopyTo(ms);
                _store.GetOrAdd(key, ms.ToArray());
                return Task.FromResult(key);
            }

            var bytes = new byte[length];
            stream.Read(bytes, 0, bytes.Length);
            _store.GetOrAdd(key, bytes);
            return Task.FromResult(key);
        }

        public virtual Task Delete(string key)
        {
            byte[] removed;
            _store.TryRemove(key, out removed);
            return Tasks.Completed;
        }
    }
}
