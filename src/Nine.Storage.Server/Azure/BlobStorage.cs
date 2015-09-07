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

        private readonly LazyAsync<CloudBlobContainer> _container;
        private readonly Lazy<Uri> _baseUri;

        public CloudBlobContainer Container => _container.GetValueAsync().Result;

        public Uri BaseUri => _baseUri.Value;

        public string ContainerName { get; private set; }
        public bool Cache { get; set; } = true;

        public TimeSpan SlidingExpiration
        {
            get { return policy.SlidingExpiration; }
            set { policy.SlidingExpiration = value; }
        }

        private BlobStorage(Func<Task<CloudBlobContainer>> container)
        {
            if (container == null) throw new ArgumentNullException("container");

            _container = new LazyAsync<CloudBlobContainer>(container);

            // Turn container into a directory
            _baseUri = new Lazy<Uri>(() => new Uri(Container.Uri + "/"));
        }

        public BlobStorage(string connectionString, string containerName = "blobs", bool publicAccess = false)
            : this(CloudStorageAccount.Parse(connectionString), containerName, publicAccess)
        {
            this.ContainerName = ContainerName;
        }

        public BlobStorage(CloudStorageAccount storageAccount, string containerName = "blobs", bool publicAccess = false)
            : this(() => ContainerFromStorageAccount(storageAccount, containerName, publicAccess))
        {
            this.ContainerName = ContainerName; 
        }

        private async static Task<CloudBlobContainer> ContainerFromStorageAccount(CloudStorageAccount storageAccount, string containerName, bool publicAccess)
        {
            // Azure blob storage doesn't like names starting with upper case letters
            containerName = containerName.ToLowerInvariant();

            var permissions = new BlobContainerPermissions { PublicAccess = publicAccess ? BlobContainerPublicAccessType.Blob : BlobContainerPublicAccessType.Off };
            var container = storageAccount.CreateCloudBlobClient().GetContainerReference(containerName);

            await container.CreateIfNotExistsAsync().ConfigureAwait(false);
            await container.SetPermissionsAsync(permissions).ConfigureAwait(false);

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

            var container = await _container.GetValueAsync().ConfigureAwait(false);

            return await container.GetBlockBlobReference(key).ExistsAsync().ConfigureAwait(false);
        }

        public async Task<Stream> Get(string key, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var cached = contentCache.Get(key) as byte[];
            if (cached != null) return new MemoryStream(cached);

            var container = await _container.GetValueAsync().ConfigureAwait(false);

            using (var stream = await container.GetBlockBlobReference(key).OpenReadAsync(cancellationToken).ConfigureAwait(false))
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

            var container = await _container.GetValueAsync().ConfigureAwait(false);

            var blob = container.GetBlockBlobReference(key);

            await blob.UploadFromStreamAsync(stream, cancellationToken).ConfigureAwait(false);

            return key;
        }
    }
}
