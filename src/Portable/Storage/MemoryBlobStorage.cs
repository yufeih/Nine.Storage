namespace Nine.Storage
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    public class MemoryBlobStorage : IBlobStorage
    {
        private readonly ConcurrentDictionary<string, byte[]> store = new ConcurrentDictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        public Task<bool> Exists(string sha)
        {
            return Task.FromResult(store.ContainsKey(sha));
        }

        public Task<string> GetUri(string sha)
        {
            return Task.FromResult<string>(null);
        }

        public Task<Stream> Get(string sha, int index, int count, IProgress<ProgressInBytes> progress = null, CancellationToken cancellation = default(CancellationToken))
        {
            byte[] bytes;
            return Task.FromResult<Stream>(store.TryGetValue(sha, out bytes) ? new MemoryStream(bytes) : null);
        }

        public Task<string> Put(Stream stream, string sha, int index, int count, IProgress<ProgressInBytes> progress = null, CancellationToken cancellation = default(CancellationToken))
        {
            var bytes = new byte[stream.Length];
            stream.Read(bytes, 0, (int)stream.Length);
            sha = Sha1.ComputeHashString(bytes);
            store.GetOrAdd(sha, bytes);
            return Task.FromResult(sha);
        }
    }
}
