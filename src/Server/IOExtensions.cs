namespace System
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    static class IOExtensions
    {
        public static async Task<byte[]> ReadBytesAsync(this Stream input, int bufferSize = 8 * 1024, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (input == null) throw new ArgumentNullException("input");

            // http://stackoverflow.com/questions/221925/creating-a-byte-array-from-a-stream
            byte[] buffer = new byte[bufferSize];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        public static async Task CopyToAsync(this Stream input, Stream output, int bufferSize, IProgress<Tuple<int, int>> progress, int totalBytes = 0)
        {
            var currentBytes = 0;
            var buffer = new byte[bufferSize];

            if (totalBytes <= 0 && input.CanSeek)
            {
                try
                {
                    totalBytes = (int)input.Length;
                }
                catch (NotSupportedException) { }
            }

            if (progress != null)
            {
                progress.Report(Tuple.Create(currentBytes, Math.Max(currentBytes, totalBytes)));
            }

            while (true)
            {
                var bytesRead = await input.ReadAsync(buffer, 0, bufferSize).ConfigureAwait(false);

                if (bytesRead <= 0)
                {
                    if (progress != null)
                    {
                        progress.Report(Tuple.Create(currentBytes, Math.Max(currentBytes, totalBytes)));
                    }
                    return;
                }

                currentBytes += bytesRead;

                await output.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);

                if (progress != null)
                {
                    progress.Report(Tuple.Create(currentBytes, Math.Max(currentBytes, totalBytes)));
                }
            }
        }

        public static async Task CopyToAsync(this Stream input, Stream output, int bufferSize, int maxSizeInBytes, Action onExceededMaxSize = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var totalBytes = 0;
            var buffer = new byte[bufferSize];

            while (true)
            {
                var bytesRead = await input.ReadAsync(buffer, 0, bufferSize, cancellationToken).ConfigureAwait(false);
                if (bytesRead <= 0)
                {
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();

                if ((totalBytes += bytesRead) > maxSizeInBytes)
                {
                    if (onExceededMaxSize != null)
                    {
                        onExceededMaxSize();
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException("Input stream is bigger than the upper bound");
                    }
                }

                await output.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
