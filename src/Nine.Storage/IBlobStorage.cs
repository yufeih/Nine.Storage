namespace Nine.Storage
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public struct ProgressInBytes
    {
        public readonly int Bytes;
        public readonly int TotalBytes;

        public float Percentage => TotalBytes > 0 ? (float)Bytes / TotalBytes : 0;

        public ProgressInBytes(int bytes, int totalBytes)
        {
            this.Bytes = bytes;
            this.TotalBytes = totalBytes;
        }
    }

    public enum BlobProgressState
    {
        None,
        Running,
        Succeeded,
        Failed,
    }

    public struct BlobProgress
    {
        public readonly string Uri;
        public readonly BlobProgressState State;
        public readonly ProgressInBytes Progress;

        public BlobProgress(string uri, BlobProgressState state, ProgressInBytes progress)
        {
            this.Uri = uri;
            this.State = state;
            this.Progress = progress;
        }
    }

    public interface IBlobStorage
    {
        Task<bool> Exists(string key);

        Task<string> GetUri(string key);

        Task<Stream> Get(string key, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken));

        Task<string> Put(string key, Stream stream, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken));
    }

    /// <summary>
    /// Represents a basic content addressable storage.
    /// </summary>
    public interface IContentAddressableStorage : IBlobStorage
    {
        Task<string> Put(Stream stream, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken));
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class BlobStorageExtensions
    {
        public static async Task<string> Download(this IBlobStorage blob, string key, CancellationToken cancellationToken = default(CancellationToken))
        {
            await blob.Get(key, null, cancellationToken).ConfigureAwait(false);
            return await blob.GetUri(key).ConfigureAwait(false);
        }

        public static async Task Download(this IBlobStorage blob, string key, Action<BlobProgress> progress, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(key)) return;

            progress(new BlobProgress(null, BlobProgressState.Running, default(ProgressInBytes)));
            
            try
            {
                var sizeInBytes = 0;
                var progressReporter = new Progress<ProgressInBytes>(p => progress(new BlobProgress(null, BlobProgressState.Running, p)));
                using (var stream = await blob.Get(key, progressReporter, cancellationToken).ConfigureAwait(false))
                {
                    try
                    {
                        sizeInBytes = (int)stream.Length;
                    }
                    catch (NotSupportedException) { }
                }

                var uri = await blob.GetUri(key).ConfigureAwait(false);
                progress(new BlobProgress(uri, BlobProgressState.Succeeded, new ProgressInBytes(sizeInBytes, sizeInBytes)));
            }
            catch
            {
                progress(new BlobProgress(null, BlobProgressState.Failed, default(ProgressInBytes)));
            }
        }
    }
}
