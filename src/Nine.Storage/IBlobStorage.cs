namespace Nine.Storage
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public struct ProgressInBytes
    {
        public int Bytes;
        public int TotalBytes;

        public float Percentage
        {
            get { return TotalBytes > 0 ? (float)Bytes / TotalBytes : 0; } 
        }
    }

    public enum BlobProgressState
    {
        None,
        Running,
        Succeeded,
        Failed,
    }

    public class BlobProgress
    {
        public string Uri { get; set; }
        public BlobProgressState State { get; set; }
        public ProgressInBytes Progress { get; set; }
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
        public static async Task<string> Download(this IBlobStorage blob, string sha, CancellationToken cancellationToken = default(CancellationToken))
        {
            await blob.Get(sha, null, cancellationToken).ConfigureAwait(false);
            return await blob.GetUri(sha).ConfigureAwait(false);
        }

        public static async Task Download(this IBlobStorage blob, string sha, Action<BlobProgress> progress, CancellationToken cancellationToken = default(CancellationToken))
        {
            var info = new BlobProgress { State = BlobProgressState.Running };
            progress(info);

            if (string.IsNullOrEmpty(sha))
            {
                info.State = BlobProgressState.Failed;
                progress(info);
                return;
            }

            try
            {
                var progressInBytes = new Progress<ProgressInBytes>(p => { info.Progress = p; progress(info); });
                await blob.Get(sha, progressInBytes, cancellationToken).ConfigureAwait(false);
                info.Uri = await blob.GetUri(sha).ConfigureAwait(false);
                info.State = BlobProgressState.Succeeded;
                progress(info);
            }
            catch
            {
                info.State = BlobProgressState.Failed;
                progress(info);
            }
        }
    }
}
