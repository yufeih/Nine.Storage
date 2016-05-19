#if !PCL
namespace Nine.Storage.Blobs
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public class FileBlobStorage : IBlobStorage
    {
        private readonly string _baseDirectory;

        public FileBlobStorage(string baseDirectory = "Blobs")
        {
            if (string.IsNullOrEmpty(baseDirectory)) throw new ArgumentException(nameof(baseDirectory));

            _baseDirectory = baseDirectory;
        }

        public Task<bool> Exists(string key)
        {
            if (string.IsNullOrEmpty(key)) return CommonTasks.False;

            return File.Exists(GetFilePath(key)) ? CommonTasks.True : CommonTasks.False;
        }

        public Task<string> GetUri(string key)
        {
            if (string.IsNullOrEmpty(key)) return CommonTasks.NullString;

            return Task.FromResult(Path.GetFullPath(GetFilePath(key)));
        }

        public Task<Stream> Get(string key, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(key)) return CommonTasks.NullStream;

            var path = GetFilePath(key);

            if (!File.Exists(path)) return CommonTasks.NullStream;

            return Task.FromResult<Stream>(File.OpenRead(path));
        }

        public Task<string> Put(string key, Stream stream, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (stream == null || key == null) return CommonTasks.NullString;

            var tempId = "." + Guid.NewGuid().ToString("N").Substring(0, 5) + ".tmp";

            var tempPath = GetFilePath(key + tempId);

            var directory = Path.GetDirectoryName(tempPath);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            using (var output = File.Create(tempPath))
            {
                stream.CopyTo(output);
            }

            try
            {
                var filename = tempPath.Substring(0, tempPath.Length - tempId.Length);

                File.Move(tempPath, filename);
            }
            catch
            {
                // This can happen if multiple threads trying to do the remove at the same time.
            }
            finally
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch { }
            }

            return Task.FromResult(key);
        }

        public Task Delete(string key)
        {
            var path = GetFilePath(key);

            if (File.Exists(path)) File.Delete(path);

            return CommonTasks.Completed;
        }

        public Task DeleteAll()
        {
            Directory.Delete(_baseDirectory, true);

            return CommonTasks.Completed;
        }

        private string GetFilePath(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length < 2) return null;
            
            return Path.Combine(_baseDirectory, key.Substring(0, 2), key);
        }
    }
}
#endif