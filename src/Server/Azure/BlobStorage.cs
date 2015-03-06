namespace Nine.Storage
{
    using System;
    using System.IO;
    using System.Runtime.Caching;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;

    public class BlobStorage : IBlobStorage
    {
        private static readonly MemoryCache contentCache = new MemoryCache(typeof(BlobStorage).Name);

        public readonly CloudBlobContainer Container;

        public Uri BaseUri { get; private set; }
        public string ContainerName { get; private set; }

        private BlobStorage(CloudBlobContainer container)
        {
            if (container == null) throw new ArgumentNullException("container");

            // Turn container into a directory
            this.BaseUri = new Uri(container.Uri + "/");
            this.Container = container;
        }

        public BlobStorage(string connectionString, string containerName = "blobs", bool publicAccess = false)
            : this(CloudStorageAccount.Parse(connectionString), containerName, publicAccess)
        {
            this.ContainerName = ContainerName;
        }

        public BlobStorage(CloudStorageAccount storageAccount, string containerName = "blobs", bool publicAccess = false)
            : this(ContainerFromStorageAccount(storageAccount, containerName, publicAccess))
        {
            this.ContainerName = ContainerName; 
        }

        private static CloudBlobContainer ContainerFromStorageAccount(CloudStorageAccount storageAccount, string containerName, bool publicAccess)
        {
            // Azure blob storage doesn't like names starting with upper case letters
            containerName = containerName.ToLowerInvariant();

            var container = storageAccount.CreateCloudBlobClient().GetContainerReference(containerName);
            container.CreateIfNotExists();
            container.SetPermissions(new BlobContainerPermissions { PublicAccess = publicAccess ? BlobContainerPublicAccessType.Blob : BlobContainerPublicAccessType.Off });
            return container;
        }

        public Task<string> GetUri(string sha1)
        {
            if (string.IsNullOrEmpty(sha1)) return Task.FromResult<string>(null);
            return Task.FromResult(BaseUri + sha1);
        }

        public async Task<bool> Exists(string sha1)
        {
            VerifySha1(sha1);

            if (contentCache.Contains(sha1)) return true;

            return await Container.GetBlockBlobReference(sha1).ExistsAsync().ConfigureAwait(false);
        }

        public async Task<Stream> Get(string sha1, int index, int count, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            VerifySha1(sha1);

            var cached = contentCache.Get(sha1) as byte[];
            if (cached != null) return new MemoryStream(cached);
            
            using (var stream = await Container.GetBlockBlobReference(sha1).OpenReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var bytes = await stream.ReadBytesAsync(8 * 1024, cancellationToken).ConfigureAwait(false);
                contentCache.Add(sha1, bytes, new DateTimeOffset(DateTime.UtcNow.AddMinutes(10)));
                return new MemoryStream(bytes);
            }
        }

        public Task<string> Put(Stream stream, string sha1, int index, int count, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Put(stream, sha1, true, int.MaxValue, null, cancellationToken);
        }

        public async Task<string> Put(Stream stream, string sha1, bool cache, int maxSizeInBytes, Action onExceededMaxSize = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            CloudBlockBlob blob;

            if (!string.IsNullOrEmpty(sha1))
            {
                blob = Container.GetBlockBlobReference(sha1);
                if (await blob.ExistsAsync(cancellationToken)) return sha1;
            }

            var buffer = new MemoryStream(8192);
            await stream.CopyToAsync(buffer, 8192, maxSizeInBytes, null, cancellationToken);

            if (string.IsNullOrEmpty(sha1))
            {
                buffer.Seek(0, SeekOrigin.Begin);
                sha1 = Sha1.ComputeHashString(buffer);
            }

            if (cache)
            {
                contentCache.Add(sha1, buffer.ToArray(), new DateTimeOffset(DateTime.UtcNow.AddMinutes(10)));
            }

            buffer.Seek(0, SeekOrigin.Begin);
            blob = Container.GetBlockBlobReference(sha1);
            await blob.UploadFromStreamAsync(buffer, cancellationToken).ConfigureAwait(false);

            return sha1;
        }

        private void VerifySha1(string sha1)
        {
            if (sha1 == null) throw new ArgumentNullException("sha1");
            if (sha1.Length != 40) throw new ArgumentException("sha1");

            foreach (var c in sha1)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'z'))) throw new ArgumentException("sha1");
            }
        }
    }
}
