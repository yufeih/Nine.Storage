namespace Nine.Storage
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Nine.Formatting;
    using PCLStorage;

    [Obsolete]
    public class PersistedStorage<T> : IStorage<T> where T : class, IKeyed, new()
    {
        private readonly Lazy<Task<PersistedStorageCore<T>>> coreFactory;

        public PersistedStorage(IFormatter formatter, string baseDirectory = "Objects")
        {
            coreFactory = new Lazy<Task<PersistedStorageCore<T>>>(() => PersistedStorageCore<T>.GetOrCreateAsync(baseDirectory, formatter));
        }

        public async Task<bool> Add(string key, T value)
        {
            var core = await coreFactory.Value.ConfigureAwait(false);
            return await core.Add(key, value).ConfigureAwait(false);
        }

        public async Task<bool> Delete(string key)
        {
            var core = await coreFactory.Value.ConfigureAwait(false);
            return await core.Delete(key).ConfigureAwait(false);
        }

        public async Task<T> Get(string key)
        {
            var core = await coreFactory.Value.ConfigureAwait(false);
            return await core.Get(key).ConfigureAwait(false);
        }

        public async Task Put(string key, T value)
        {
            var core = await coreFactory.Value.ConfigureAwait(false);
            await core.Put(key, value).ConfigureAwait(false);
        }

        public async Task<IEnumerable<T>> Range(string minKey, string maxKey, int? count = default(int?))
        {
            var core = await coreFactory.Value.ConfigureAwait(false);
            return await core.Range(minKey, maxKey, count).ConfigureAwait(false);
        }
    }

    class PersistedStorageCore<T> : IStorage<T> where T : class, IKeyed, new()
    {
        struct Node
        {
            public Bucket Bucket;
            public int Index;
        };

        class Bucket
        {
            public int Size;
            public int Count;
            public byte[] Buffer;
            public Stream Stream;
        }

        class KeyComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[] x, byte[] y)
            {
                var i = x.Length;
                if (i != y.Length) return false;
                while (--i >= 0) if (x[i] != y[i]) return false;
                return true;
            }

            public int GetHashCode(byte[] obj)
            {
                var length = obj.Length;
                return (obj[0] << 8) | (obj[length - 1]);
            }
        }

        private const int HeaderSize = 2;
        private readonly object sync = new object();
        private readonly string baseDirectory;
        private readonly IFormatter formatter;
        private readonly Encoding encoding = new UTF8Encoding(false, false);
        private readonly List<Bucket> buckets = new List<Bucket>();
        private readonly Dictionary<byte[], Node> items = new Dictionary<byte[], Node>(new KeyComparer());

        private static ConcurrentDictionary<string, Lazy<Task<PersistedStorageCore<T>>>> instances = new ConcurrentDictionary<string, Lazy<Task<PersistedStorageCore<T>>>>();

        private PersistedStorageCore(string baseDirectory, IFormatter formatter)
        {
            if (formatter == null) throw new ArgumentNullException(nameof(formatter));
            if (baseDirectory != null && Path.IsPathRooted(baseDirectory)) throw new NotSupportedException();

            this.formatter = formatter;
            this.baseDirectory = PortablePath.Combine(baseDirectory, typeof(T).Name);
        }

        public static Task<PersistedStorageCore<T>> GetOrCreateAsync(string baseDirectory, IFormatter formatter)
        {
            var instance = instances.GetOrAdd(baseDirectory, k => new Lazy<Task<PersistedStorageCore<T>>>(() => CreateAsync(baseDirectory, formatter)));
            return instance.Value;
        }

        private static async Task<PersistedStorageCore<T>> CreateAsync(string baseDirectory, IFormatter formatter)
        {
            var result = new PersistedStorageCore<T>(baseDirectory, formatter);
            try
            {
                await result.InitializeAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
            return result;
        }

        private async Task InitializeAsync()
        {
            var folder = await FileSystem.Current.LocalStorage.CreateFolderAsync(baseDirectory, CreationCollisionOption.OpenIfExists);
            if (folder == null) return;

            foreach (var file in await folder.GetFilesAsync())
            {
                try
                {
                    var bucketSize = 0;
                    if (!int.TryParse(file.Name, out bucketSize) || bucketSize <= 0) continue;

                    var stream = await PersistedStorageSharedStreams.OpenStreamAsync(PortablePath.Combine(baseDirectory, file.Name)).ConfigureAwait(false);

                    lock (sync)
                    {
                        var bucket = new Bucket { Size = bucketSize, Buffer = new byte[bucketSize] };
                        bucket.Stream = stream;
                        buckets.Add(bucket);

                        for (int i = 0; i * bucket.Size < bucket.Stream.Length; i++)
                        {
                            try
                            {
                                var node = new Node { Bucket = bucket, Index = i };
                                var value = FromNode(node);
                                if (value != null)
                                {
                                    var key = CompressString(value.GetKey());
                                    if (!items.ContainsKey(key)) items.Add(key, node);
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.WriteLine(e);
                            }
                            finally
                            {
                                bucket.Count++;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            }
        }

        private async Task<Stream> OpenStreamForWriteAsync(int bucket)
        {
            await FileSystem.Current.LocalStorage.CreateFolderAsync(baseDirectory, CreationCollisionOption.OpenIfExists).ConfigureAwait(false);
            return await PersistedStorageSharedStreams.OpenStreamAsync(PortablePath.Combine(baseDirectory, bucket.ToString())).ConfigureAwait(false);
        }

        public Task<T> Get(string key)
        {
            lock (sync)
            {
                Node node;
                if (items.TryGetValue(CompressString(key), out node)) return Task.FromResult(FromNode(node));
                return Task.FromResult<T>(null);
            }
        }

        public Task<IEnumerable<T>> Range(string minKey, string maxKey, int? count = null)
        {
            lock (sync)
            {
                var result =
                    from x in items
                    let uncompressedKey = UncompressString(x.Key)
                    where (minKey == null || string.CompareOrdinal(uncompressedKey, minKey) >= 0) &&
                          (maxKey == null || string.CompareOrdinal(uncompressedKey, maxKey) < 0)
                    orderby uncompressedKey
                    select FromNode(x.Value);

                if (count != null)
                {
                    result = result.Take(count.Value);
                }

                return Task.FromResult<IEnumerable<T>>(result.ToArray());
            }
        }

        public Task Put(string key, T value)
        {
            lock (sync)
            {
                Delete(key);
                return Add(key, value);
            }
        }

        public Task<bool> Add(string key, T value)
        {
            var compactKey = CompressString(key);

            var bytes = formatter.ToBytes(value);
            var count = bytes.Length;
            if (count > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException("Object is too big for PersistedStorage");
            }

            var head = BitConverter.GetBytes((ushort)count);
            var bucket = GetBucket(count + HeaderSize);

            lock (sync)
            {
                if (items.ContainsKey(compactKey)) return Task.FromResult(false);

                Node node = new Node { Bucket = bucket, Index = bucket.Count };
                bucket.Count++;

                Buffer.BlockCopy(head, 0, bucket.Buffer, 0, HeaderSize);
                Buffer.BlockCopy(bytes, 0, bucket.Buffer, HeaderSize, count);

                bucket.Stream.Seek(node.Index * bucket.Size, SeekOrigin.Begin);
                bucket.Stream.Write(bucket.Buffer, 0, count + HeaderSize);
                bucket.Stream.Flush();

                items.Add(compactKey, node);
            }

            return Task.FromResult(true);
        }

        public Task<bool> Delete(string key)
        {
            var compactKey = CompressString(key);

            lock (sync)
            {
                Node node;

                if (!items.TryGetValue(compactKey, out node)) return Task.FromResult(false);

                var bucket = node.Bucket;

                bucket.Count--;

                if (bucket.Count > 0)
                {
                    var lastNode = FromNode(new Node { Bucket = bucket, Index = bucket.Count });
                    var lastKey = lastNode != null ? CompressString(lastNode.GetKey()) : null;

                    bucket.Stream.Seek(bucket.Count * bucket.Size, SeekOrigin.Begin);
                    bucket.Stream.Read(bucket.Buffer, 0, bucket.Size);

                    bucket.Stream.Seek(node.Index * bucket.Size, SeekOrigin.Begin);
                    bucket.Stream.Write(bucket.Buffer, 0, bucket.Size);

                    if (lastKey != null && items.ContainsKey(lastKey))
                    {
                        items[lastKey] = new Node { Bucket = bucket, Index = node.Index };
                    }
                }

                bucket.Stream.SetLength(bucket.Count * bucket.Size);
                bucket.Stream.Flush();

                items.Remove(compactKey);
            }

            return Task.FromResult(true);
        }

        private T FromNode(Node node)
        {
            lock (sync)
            {
                var bucket = node.Bucket;
                bucket.Stream.Seek(node.Index * bucket.Size, SeekOrigin.Begin);
                bucket.Stream.Read(bucket.Buffer, 0, bucket.Size);

                var count = (int)BitConverter.ToUInt16(bucket.Buffer, 0);
                if (count > 0 && count <= bucket.Size - HeaderSize)
                {
                    return formatter.FromBytes<T>(bucket.Buffer, HeaderSize, count);
                }
                return null;
            }
        }

        private Bucket GetBucket(int length)
        {
            length = UpperPowerOfTwo(length);

            lock (sync)
            {
                for (var i = 0; i < buckets.Count; i++)
                {
                    if (buckets[i].Size == length) return buckets[i];
                }

                var bucket = new Bucket { Size = length, Buffer = new byte[length], Stream = OpenStreamForWriteAsync(length).Result };
                buckets.Add(bucket);
                return bucket;
            }
        }

        private byte[] CompressString(string value)
        {
            return encoding.GetBytes(value);
        }

        private string UncompressString(byte[] value)
        {
            return encoding.GetString(value, 0, value.Length);
        }

        private static int UpperPowerOfTwo(int v)
        {
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;
            return v;
        }
    }

    static class PersistedStorageSharedStreams
    {
        private static readonly Dictionary<string, Lazy<Task<Stream>>> sharedStreams = new Dictionary<string, Lazy<Task<Stream>>>(StringComparer.OrdinalIgnoreCase);

        public static Task<Stream> OpenStreamAsync(string path)
        {
            Lazy<Task<Stream>> result;
            lock (sharedStreams)
            {
                if (!sharedStreams.TryGetValue(path, out result))
                {
                    sharedStreams.Add(path, result = new Lazy<Task<Stream>>(() => OpenStreamCoreAsync(path)));
                }
            }
            return result.Value;
        }

        private static async Task<Stream> OpenStreamCoreAsync(string path)
        {
            var file = await FileSystem.Current.LocalStorage.CreateFileAsync(path, CreationCollisionOption.OpenIfExists).ConfigureAwait(false);
            if (file == null) return null;
            return await file.OpenAsync(PCLStorage.FileAccess.ReadAndWrite).ConfigureAwait(false);
        }
    }
}