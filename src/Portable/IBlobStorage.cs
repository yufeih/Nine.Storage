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
        Task<bool> Exists(string sha);

        Task<string> GetUri(string sha);

        Task<Stream> Get(string sha, int index, int count, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken));

        Task<string> Put(Stream stream, string sha, int index, int totalLength, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken));
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class BlobStorageExtensions
    {
        public static Task<Stream> Get(this IBlobStorage blob, string sha, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return blob.Get(sha, 0, -1, progress, cancellationToken);
        }

        public static Task<string> Put(this IBlobStorage blob, Stream stream, string sha = null, IProgress<ProgressInBytes> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return blob.Put(stream, sha, 0, -1, progress, cancellationToken);
        }

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
