namespace Nine.Storage
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using PCLStorage;

    public class FileBlobStorage : IBlobStorage
    {
        private readonly string baseDirectory;
        private readonly ConcurrentDictionary<string, LazyAsync<string>> puts = new ConcurrentDictionary<string, LazyAsync<string>>(StringComparer.OrdinalIgnoreCase);

        public FileBlobStorage(string baseDirectory = "Blobs")
        {
            if (baseDirectory != null && Path.IsPathRooted(baseDirectory))
            {
                throw new NotSupportedException();
            }

            this.baseDirectory = baseDirectory;
        }

        public async Task<bool> Exists(string sha)
        {
            return await GetFileAsync(sha).ConfigureAwait(false) != null;
        }

        public async Task<string> GetUri(string sha)
        {
            var file = await GetFileAsync(sha).ConfigureAwait(false);
            return file != null ? file.Path : null;
        }

        public async Task<Stream> Get(string sha, int index, int count, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var file = await GetFileAsync(sha, cancellationToken).ConfigureAwait(false);
            return file != null ? await file.OpenAsync(FileAccess.Read, cancellationToken).ConfigureAwait(false) : null;
        }

        public async Task<string> Put(Stream stream, string sha, int index, int count, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (stream == null) return null;

            if (string.IsNullOrEmpty(sha))
            {
                sha = Sha1.ComputeHashString(stream);
                stream.Seek(0, SeekOrigin.Begin);
            }

            if (await GetFileAsync(sha, cancellationToken).ConfigureAwait(false) != null)
            {
                return sha;
            }

            var result = await puts.GetOrAdd(sha, k => new LazyAsync<string>(() => PutCoreAsync(stream, sha, progress), true)).GetValueAsync().ConfigureAwait(false);

            LazyAsync<string> temp;
            puts.TryRemove(sha, out temp);
            return result; 
        }

        private async Task<string> PutCoreAsync(Stream stream, string sha, IProgress<ProgressInBytes> progress)
        {
            var tempId = "." + Guid.NewGuid().ToString("N").Substring(0, 5) + ".tmp";
            var tempFile = await CreateFileIfNotExistAsync(sha + tempId).ConfigureAwait(false);
            using (var output = await tempFile.OpenAsync(FileAccess.ReadAndWrite).ConfigureAwait(false))
            {
                await stream.CopyToAsync(output).ConfigureAwait(false);
            }

            // Check again if someone else has already got the file
            if (await GetFileAsync(sha) != null) return sha;

            var failed = true;

            try
            {
                var filename = tempFile.Path.Substring(0, tempFile.Path.Length - tempId.Length);
                await tempFile.MoveAsync(filename).ConfigureAwait(false);
                failed = false;
            }
            // This can happen if multiple threads trying to do the remove at the same time.
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (Exception e) { Debug.WriteLine(e); }

            if (failed)
            {
                try
                {
                    await tempFile.DeleteAsync().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            }

            return sha;
        }

        public async Task DeleteAsync(string sha)
        {
            var file = await GetFileAsync(sha).ConfigureAwait(false);
            if (file != null) await file.DeleteAsync();
        }

        public async Task DeleteAllAsync()
        {
            var path = FileSystem.Current.LocalStorage.Path;
            if (!string.IsNullOrEmpty(baseDirectory))
            {
                path = PortablePath.Combine(path, baseDirectory);
            }

            var folder = await FileSystem.Current.GetFolderFromPathAsync(path).ConfigureAwait(false);
            if (folder != null) await folder.DeleteAsync().ConfigureAwait(false);
        }

        private async Task<IFile> GetFileAsync(string sha, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                var path = GetUriCore(sha);
                if (string.IsNullOrEmpty(path)) return null;
                return await FileSystem.Current.GetFileFromPathAsync(path, cancellationToken).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }

        private async Task<IFile> CreateFileIfNotExistAsync(string sha)
        {
            if (sha == null || sha.Length < 2)
            {
                throw new ArgumentException("sha");
            }

            var path = sha.Substring(0, 2);
            if (!string.IsNullOrEmpty(baseDirectory))
            {
                path = PortablePath.Combine(baseDirectory, path);
            }

            var storage = FileSystem.Current.LocalStorage;
            await storage.CreateFolderAsync(path, CreationCollisionOption.OpenIfExists).ConfigureAwait(false);

            var filename = PortablePath.Combine(path, sha);
            return await storage.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting).ConfigureAwait(false);
        }

        private string GetUriCore(string sha)
        {
            if (string.IsNullOrEmpty(sha) || sha.Length < 2) return null;

            var path = PortablePath.Combine(sha.Substring(0, 2), sha);
            if (!string.IsNullOrEmpty(baseDirectory))
            {
                path = PortablePath.Combine(baseDirectory, path);
            }

            return PortablePath.Combine(FileSystem.Current.LocalStorage.Path, path);
        }
    }
}
