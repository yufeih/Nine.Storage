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
        private readonly CacheItemPolicy policy = new CacheItemPolicy { SlidingExpiration = TimeSpan.FromMinutes(30) };
        private readonly MemoryCache contentCache = new MemoryCache(typeof(BlobStorage).Name);

        public readonly CloudBlobContainer Container;

        public Uri BaseUri { get; private set; }
        public string ContainerName { get; private set; }
        public bool Cache { get; set; } = true;

        public TimeSpan SlidingExpiration
        {
            get { return policy.SlidingExpiration; }
            set { policy.SlidingExpiration = value; }
        }

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
                if (Cache)
                {
                    contentCache.Add(key, bytes, policy);
                }
                return new MemoryStream(bytes);
            }
        }

        public async Task<string> Put(string key, Stream stream, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (Cache)
            {
                var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                contentCache.Add(key, ms.ToArray(), policy);
                ms.Seek(0, SeekOrigin.Begin);
                stream = ms;
            }
            
            var blob = Container.GetBlockBlobReference(key);
            await blob.UploadFromStreamAsync(stream, cancellationToken).ConfigureAwait(false);

            return key;
        }
    }
}
