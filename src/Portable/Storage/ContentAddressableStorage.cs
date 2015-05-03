namespace Nine.Storage
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    public class ContentAddressableStorage : IContentAddressableStorage
    {
        private readonly IBlobStorage blob;

        public IBlobStorage Blob => blob;

        public ContentAddressableStorage(IBlobStorage blob)
        {
            if ((this.blob = blob) == null) throw new ArgumentNullException(nameof(blob));
        }

        public Task<bool> Exists(string key) => blob.Exists(VerifySha1(key));
        public Task<string> GetUri(string key) => blob.GetUri(VerifySha1(key));
        public Task<Stream> Get(string key, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken)) => blob.Get(VerifySha1(key), progress, cancellationToken);
        public Task<string> Put(Stream stream, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken)) => Put(null, stream, progress, cancellationToken);

        public Task<string> Put(string key, Stream stream, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(key))
            {
                key = Sha1.ComputeHashString(stream);
                stream.Seek(0, SeekOrigin.Begin);
            }
            return blob.Put(key, stream, progress, cancellationToken);
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
