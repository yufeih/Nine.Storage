namespace Nine.Storage.Syncing
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;

    public class SyncSource : ISyncSource
    {
        private readonly SynchronizationContext _sync;
        private readonly Func<Type, IEnumerable<ISyncSource>> _selector;

        public SyncSource(params ISyncSource[] sources) : this(x => sources) { }
        public SyncSource(Func<Type, ISyncSource> selector) : this(x => new[] { selector(x) }) { }
        public SyncSource(Func<Type, IEnumerable<ISyncSource>> selector)
        {
            if (selector == null) throw new ArgumentNullException("selector");

            _selector = selector;
            _sync = SynchronizationContext.Current;

            if (_sync == null)
            {
                Debug.WriteLine("Failed to capture SynchronizationContext");
            }
        }

        public IDisposable On<T>(Action<Delta<T>> action)
        {
            return On<T>(source => source.On(RunOnSynchronizationContext(action)));
        }

        public IDisposable On<T>(string key, Action<Delta<T>> action)
        {
            return On<T>(source => source.On(key, RunOnSynchronizationContext(action)));
        }

        private IDisposable On<T>(Func<ISyncSource, IDisposable> action)
        {
            var sources = _selector(typeof(T));
            if (sources == null || !sources.Any()) return null;
            return new Disposable(sources.Select(action));
        }

        private Action<Delta<T>> RunOnSynchronizationContext<T>(Action<Delta<T>> action)
        {
            return new Action<Delta<T>>(x =>
            {
                if (_sync == null)
                {
                    action(x);
                }
                else
                {
                    _sync.Post(y => action(x), null);
                }
            });
        }

        class Disposable : IDisposable
        {
            private readonly List<IDisposable> sources;

            public Disposable(IEnumerable<IDisposable> sources)
            {
                this.sources = new List<IDisposable>(sources.OfType<IDisposable>());
            }

            public void Dispose()
            {
                foreach (var source in sources) source.Dispose();
            }
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public class SyncSource<T> : ISyncSource<T>
    {
        class Subscription : IDisposable
        {
            public readonly string Key;

            private readonly Action<Delta<T>> _action;
            private readonly SyncSource<T> _owner;

            public Subscription(string key, SyncSource<T> owner, Action<Delta<T>> action)
            {
                Key = key;
                _owner = owner;
                _action = action;
            }

            public void Notify(Delta<T> change)
            {
                _action(change);
            }

            public void Dispose()
            {
                _owner.Unsubscribe(this);
            }
        }

        private bool _notifying;
        private readonly object _sync = new object();
        private readonly Dictionary<string, List<Subscription>> _keyedSubscriptions = new Dictionary<string, List<Subscription>>();
        private readonly List<Subscription> _subscriptions = new List<Subscription>();
        private readonly List<Subscription> _abandonedSubscriptions = new List<Subscription>();

        public virtual IDisposable On(Action<Delta<T>> action)
        {
            if (action == null) throw new ArgumentNullException("action");

            lock (_sync)
            {
                var result = new Subscription(null, this, action);
                _subscriptions.Add(result);
                return result;
            }
        }

        public virtual IDisposable On(string key, Action<Delta<T>> action)
        {
            if (action == null) throw new ArgumentNullException("action");
            if (key == null) throw new ArgumentNullException("key");

            lock (_sync)
            {
                List<Subscription> subscriptions;
                var result = new Subscription(key, this, action);
                if (!_keyedSubscriptions.TryGetValue(key, out subscriptions))
                {
                    _keyedSubscriptions.Add(key, subscriptions = new List<Subscription>());
                }
                subscriptions.Add(result);
                return result;
            }
        }

        private void Unsubscribe(Subscription subscription)
        {
            lock (_sync)
            {
                if (_notifying)
                {
                    _abandonedSubscriptions.Add(subscription);
                }
                else
                {
                    Remove(subscription);
                }
            }
        }

        private void Remove(Subscription subscription)
        {
            if (subscription.Key != null)
            {
                List<Subscription> value;
                if (_keyedSubscriptions.TryGetValue(subscription.Key, out value))
                {
                    value.Remove(subscription);
                }
            }
            else
            {
                _subscriptions.Remove(subscription);
            }
        }

        protected virtual void Notify(Delta<T> change)
        {
            lock (_sync)
            {
                for (var i = 0; i < _abandonedSubscriptions.Count; i++)
                {
                    Remove(_abandonedSubscriptions[i]);
                }

                try
                {
                    _notifying = true;

                    NotifyByKey(change);
                    NotifyAll(change);
                }
                finally
                {
                    _notifying = false;
                }
            }
        }

        private void NotifyByKey(Delta<T> change)
        {
            if (_keyedSubscriptions.Count <= 0) return;

            List<Subscription> subscriptions;
            if (!_keyedSubscriptions.TryGetValue(change.Key, out subscriptions)) return;

            for (var i = 0; i < subscriptions.Count; i++)
            {
                subscriptions[i].Notify(change);
            }
        }

        private void NotifyAll(Delta<T> change)
        {
            for (var i = 0; i < _subscriptions.Count; i++)
            {
                _subscriptions[i].Notify(change);
            }
        }
    }
}
