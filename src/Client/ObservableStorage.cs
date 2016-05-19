namespace Nine.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using System.Linq;
    using Nine.Storage.Syncing;

    public class ObservableStorage<T> : SyncSource<T>, IStorage<T> where T : class, IKeyed, new()
    {
        private readonly IStorage<T> _storage;
        private readonly ConcurrentDictionary<string, WeakReference<T>> _instances;

        /// <summary>
        /// Gets or sets a value indicating whether put should modify an existing instance.
        /// This ensures that objects with the same key always shares the same object reference.
        /// </summary>
        public bool ReuseExistingInstance => _instances != null;

        public ObservableStorage(IStorage<T> storage, bool reuseExistingInstance = false)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            if (reuseExistingInstance) _instances = new ConcurrentDictionary<string, WeakReference<T>>();

            _storage = storage;
        }

        public async Task<T> Get(string key)
        {
            T target;
            WeakReference<T> wr;
            if (_instances != null && _instances.TryGetValue(key, out wr) && wr.TryGetTarget(out target))
            {
                return target;
            }

            var result = await _storage.Get(key).ConfigureAwait(false);
            if (_instances == null) return result;

            if (!_instances.TryGetValue(key, out wr) || !wr.TryGetTarget(out target))
            {
                wr = new WeakReference<T>(result);
                _instances.AddOrUpdate(key, wr, (k, v) => wr).TryGetTarget(out target);
            }
            return target;
        }

        public async Task<IEnumerable<T>> Range(string minKey, string maxKey, int? count = null)
        {
            var results = await _storage.Range(minKey, maxKey, count).ConfigureAwait(false);
            if (_instances == null) return results;

            T target;
            WeakReference<T> wr;
            IList<T> list = (results as IList<T>) ?? results.ToArray();
            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item == null) continue;

                var key = item.GetKey();
                if (!_instances.TryGetValue(key, out wr) || !wr.TryGetTarget(out target))
                {
                    wr = new WeakReference<T>(item);
                    _instances.AddOrUpdate(key, wr, (k, v) => wr).TryGetTarget(out target);
                }
                list[i] = target;
            }
            return list;
        }

        public async Task<bool> Add(string key, T value)
        {
            if (!await _storage.Add(key, value).ConfigureAwait(false)) return false;

            if (_instances != null)
            {
                T target;
                var wr = _instances.GetOrAdd(key, _ => new WeakReference<T>(value));
                if (wr.TryGetTarget(out target))
                {
                    value = ObjectHelper.Merge(target, value);
                }
            }

            Notify(new Delta<T>(DeltaAction.Add, key, value));
            return true;
        }

        public async Task Put(string key, T value)
        {
            if (_instances != null)
            {
                T target;
                var wr = _instances.GetOrAdd(key, _ => new WeakReference<T>(value));
                if (wr.TryGetTarget(out target))
                {
                    value = ObjectHelper.Merge(target, value);
                }
            }

            await _storage.Put(value).ConfigureAwait(false);
            Notify(new Delta<T>(DeltaAction.Put, key, value));
        }

        public async Task<bool> Delete(string key)
        {
            WeakReference<T> wr;
            var existing = await Get(key).ConfigureAwait(false);
            if (existing == null) return false;
            if (!await _storage.Delete(key).ConfigureAwait(false)) return false;
            if (_instances != null) _instances.TryRemove(key, out wr);
            Notify(new Delta<T>(DeltaAction.Remove, key, existing));
            return true;
        }
    }
}
