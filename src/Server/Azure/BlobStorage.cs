namespace Nine.Storage
{
    using System;
    using System.IO;
    using System.Runtime.Caching;
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

        public Task<string> GetUri(string key)
        {
            if (string.IsNullOrEmpty(key)) return Task.FromResult<string>(null);
            return Task.FromResult(BaseUri + key);
        }

        public async Task<bool> Exists(string key)
        {
            if (contentCache.Contains(key)) return true;

            return await Container.GetBlockBlobReference(key).ExistsAsync().ConfigureAwait(false);
        }

        public async Task<Stream> Get(string key, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var cached = contentCache.Get(key) as byte[];
            if (cached != null) return new MemoryStream(cached);
            
            using (var stream = await Container.GetBlockBlobReference(key).OpenReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var bytes = await stream.ReadBytesAsync(8 * 1024, cancellationToken).ConfigureAwait(false);
                contentCache.Add(key, bytes, new DateTimeOffset(DateTime.UtcNow.AddMinutes(10)));
                return new MemoryStream(bytes);
            }
        }

        public Task<string> Put(string key, Stream stream, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Put(key, stream, true, int.MaxValue, null, cancellationToken);
        }

        public async Task<string> Put(string key, Stream stream, bool cache, int maxSizeInBytes, Action onExceededMaxSize = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            CloudBlockBlob blob;

            if (!string.IsNullOrEmpty(key))
            {
                blob = Container.GetBlockBlobReference(key);
                if (await blob.ExistsAsync(cancellationToken)) return key;
            }

            var buffer = new MemoryStream(8192);
            await stream.CopyToAsync(buffer, 8192, maxSizeInBytes, null, cancellationToken);
            
            if (cache)
            {
                contentCache.Add(key, buffer.ToArray(), new DateTimeOffset(DateTime.UtcNow.AddMinutes(10)));
            }

            buffer.Seek(0, SeekOrigin.Begin);
            blob = Container.GetBlockBlobReference(key);
            await blob.UploadFromStreamAsync(buffer, cancellationToken).ConfigureAwait(false);

            return key;
        }
    }
}
