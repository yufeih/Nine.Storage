namespace Nine.Storage.Blobs
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    public class ContentAddressableStorage : IContentAddressableStorage
    {
        private readonly IBlobStorage _blob;

        public IBlobStorage Blob => _blob;

        public ContentAddressableStorage(IBlobStorage blob)
        {
            if ((_blob = blob) == null) throw new ArgumentNullException(nameof(blob));
        }

        public Task<bool> Exists(string key) => _blob.Exists(VerifySha1(key));
        public Task<string> GetUri(string key) => _blob.GetUri(VerifySha1(key));
        public Task<Stream> Get(string key, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken)) => _blob.Get(VerifySha1(key), progress, cancellationToken);
        public Task<string> Put(Stream stream, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken)) => Put(null, stream, progress, cancellationToken);

        public async Task<string> Put(string key, Stream stream, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(key))
            {
                if (!stream.CanSeek)
                {
                    var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    stream = ms;
                }

                key = Sha1.ComputeHashString(stream);
                stream.Seek(0, SeekOrigin.Begin);
            }

            if (await _blob.Exists(key).ConfigureAwait(false))
            {
                return key;
            }

            return await _blob.Put(key, stream, progress, cancellationToken).ConfigureAwait(false);
        }

        private string VerifySha1(string key)
        {
            if (key == null) return null;
            if (key.Length != 40) throw new ArgumentException("key");

            foreach (var c in key)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'z'))) throw new ArgumentException("key");
            }

            return key;
        }
    }
}
