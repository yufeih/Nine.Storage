namespace Nine.Storage
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Nine.Storage.Syncing;

    public class StorageContainer : IStorage, ISyncSource
    {
        private readonly ConcurrentDictionary<Type, ConcurrentQueue<Action<object>>> _initializers 
                   = new ConcurrentDictionary<Type, ConcurrentQueue<Action<object>>>();

        private readonly StorageProvider _provider;

        private long _readCount = 0;
        private long _writeCount = 0;

        public long ReadCount => _readCount;
        public long WriteCount => _writeCount;

        public double ReadWriteRatio => _writeCount > 0 ? 1.0 * _readCount / _writeCount : 1;

        public StorageProvider StorageProvider => _provider;

        public StorageContainer(Type type) : this(x => Activator.CreateInstance(type.MakeGenericType(x)))
        { }

        public StorageContainer(Func<Type, object> factory) : this(new TypedStorageProvider(factory))
        { }

        public StorageContainer(StorageProvider storageProvider)
        {
            if (storageProvider == null) throw new ArgumentNullException(nameof(storageProvider));

            _provider = storageProvider;
        }

        public async Task<T> Get<T>(string key)
        {
            Interlocked.Increment(ref _readCount);
            var storage = await _provider.GetStorage<T>().ConfigureAwait(false);
            EnsureInitialized<T>(storage);
            return await storage.Get(key).ConfigureAwait(false);
        }

        public async Task<IEnumerable<T>> Range<T>(string minKey, string maxKey, int? maxCount = null)
        {
            Interlocked.Increment(ref _readCount);
            var storage = await _provider.GetStorage<T>().ConfigureAwait(false);
            EnsureInitialized<T>(storage);
            return await storage.Range(minKey, maxKey, maxCount).ConfigureAwait(false);
        }

        public async Task<bool> Add<T>(string key, T value)
        {
            if (value == null) throw new ArgumentNullException("value");

            if (Interlocked.Increment(ref _writeCount) > 100000)
            {
                Interlocked.Exchange(ref _writeCount, 1);
                Interlocked.Exchange(ref _readCount, 0);
            }

            var storage = await _provider.GetStorage<T>().ConfigureAwait(false);
            EnsureInitialized<T>(storage);
            return await storage.Add(key, value).ConfigureAwait(false);
        }

        public async Task Put<T>(string key, T value)
        {
            if (value == null) throw new ArgumentNullException("value");

            if (Interlocked.Increment(ref _writeCount) > 100000)
            {
                Interlocked.Exchange(ref _writeCount, 1);
                Interlocked.Exchange(ref _readCount, 0);
            }

            var storage = await _provider.GetStorage<T>().ConfigureAwait(false);
            EnsureInitialized<T>(storage);
            await storage.Put(key, value).ConfigureAwait(false);
        }

        public async Task<bool> Delete<T>(string key)
        {
            if (Interlocked.Increment(ref _writeCount) > 100000)
            {
                Interlocked.Exchange(ref _writeCount, 1);
                Interlocked.Exchange(ref _readCount, 0);
            }

            var storage = await _provider.GetStorage<T>().ConfigureAwait(false);
            EnsureInitialized<T>(storage);
            return await storage.Delete(key).ConfigureAwait(false);
        }

        public IDisposable On<T>(Action<Delta<T>> action)
        {
            var result = new OnDisposable();
            _initializers.GetOrAdd(typeof(T), type => new ConcurrentQueue<Action<object>>()).Enqueue(state =>
            {
                // Ensure we are always invoked before any other operations occured on the storage.
                var sync = state as ISyncSource<T>;
                if (sync != null && !result.disposed)
                {
                    result.inner = sync.On(action);
                }
            });
            EnsureInitialized<T>();
            return result;
        }

        public IDisposable On<T>(string key, Action<Delta<T>> action)
        {
            var result = new OnDisposable();
            _initializers.GetOrAdd(typeof(T), type => new ConcurrentQueue<Action<object>>()).Enqueue(state =>
            {
                // Ensure we are always invoked before any other operations occured on the storage.
                var sync = state as ISyncSource<T>;
                if (sync != null && !result.disposed)
                {
                    result.inner = sync.On(key, action);
                }
            });
            EnsureInitialized<T>();
            return result;
        }

        private async void EnsureInitialized<T>()
        {
            EnsureInitialized<T>(await _provider.GetStorage<T>().ConfigureAwait(false));
        }

        private void EnsureInitialized<T>(object state)
        {
            Action<object> action;
            ConcurrentQueue<Action<object>> queue;
            if (_initializers.TryGetValue(typeof(T), out queue))
            {
                while (queue.TryDequeue(out action)) action(state);
            }
        }

        class OnDisposable : IDisposable
        {
            public bool disposed;
            public IDisposable inner;

            public void Dispose()
            {
                disposed = true;
                if (inner != null) inner.Dispose();
            }
        }

        class TypedStorageProvider : StorageProvider
        {
            private readonly Func<Type, object> _factory;

            public TypedStorageProvider(Func<Type, object> factory)
            {
                if (factory == null) throw new ArgumentNullException("factory");

                _factory = factory;
            }

            protected override Task<IStorage<T>> CreateAsync<T>()
            {
                return Task.FromResult((IStorage<T>)_factory(typeof(T)));
            }
        }
    }
}
