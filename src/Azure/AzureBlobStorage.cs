namespace Nine.Storage.Blobs
{
    using System;
    using System.IO;
    using System.Runtime.Caching;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;

    public class AzureBlobStorage : IBlobStorage
    {
        private readonly CacheItemPolicy _policy = new CacheItemPolicy { SlidingExpiration = TimeSpan.FromMinutes(30) };
        private readonly MemoryCache _contentCache = new MemoryCache(typeof(AzureBlobStorage).Name);

        private readonly Lazy<Task<CloudBlobContainer>> _container;
        private readonly Lazy<Uri> _baseUri;

        private readonly string _containerName;

        public CloudBlobContainer Container => _container.Value.Result;

        public Uri BaseUri => _baseUri.Value;

        public string ContainerName => _containerName;
        public bool Cache { get; set; } = true;

        public TimeSpan SlidingExpiration
        {
            get { return _policy.SlidingExpiration; }
            set { _policy.SlidingExpiration = value; }
        }

        private AzureBlobStorage(Func<Task<CloudBlobContainer>> container)
        {
            if (container == null) throw new ArgumentNullException("container");

            _container = new Lazy<Task<CloudBlobContainer>>(container);

            // Turn container into a directory
            _baseUri = new Lazy<Uri>(() => new Uri(Container.Uri + "/"));
        }

        public AzureBlobStorage(string connectionString, string containerName = "blobs", bool publicAccess = false)
            : this(CloudStorageAccount.Parse(connectionString), containerName, publicAccess)
        {
            _containerName = containerName;
        }

        public AzureBlobStorage(CloudStorageAccount storageAccount, string containerName = "blobs", bool publicAccess = false)
            : this(() => ContainerFromStorageAccount(storageAccount, containerName, publicAccess))
        {
            _containerName = containerName; 
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

        public string GetUri(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            return BaseUri + key;
        }

        public async Task<bool> Exists(string key)
        {
            if (_contentCache.Contains(key)) return true;

            var container = await _container.Value.ConfigureAwait(false);

            return await container.GetBlockBlobReference(key).ExistsAsync().ConfigureAwait(false);
        }

        public async Task<Stream> Get(string key, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var cached = _contentCache.Get(key) as byte[];
            if (cached != null) return new MemoryStream(cached);

            var container = await _container.Value.ConfigureAwait(false);

            using (var stream = await container.GetBlockBlobReference(key).OpenReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var bytes = await stream.ReadBytesAsync(8 * 1024, cancellationToken).ConfigureAwait(false);
                if (Cache)
                {
                    _contentCache.Add(key, bytes, _policy);
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
                _contentCache.Add(key, ms.ToArray(), _policy);
                ms.Seek(0, SeekOrigin.Begin);
                stream = ms;
            }

            var container = await _container.Value.ConfigureAwait(false);

            var blob = container.GetBlockBlobReference(key);

            await blob.UploadFromStreamAsync(stream, cancellationToken).ConfigureAwait(false);

            return key;
        }

        public async Task Delete(string key)
        {
            _contentCache.Remove(key);

            var container = await _container.Value.ConfigureAwait(false);

            await container.GetBlockBlobReference(key).DeleteAsync().ConfigureAwait(false);
        }
    }
}
