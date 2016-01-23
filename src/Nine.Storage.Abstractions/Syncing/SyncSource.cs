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
        private readonly SynchronizationContext sync;
        private readonly Func<Type, IEnumerable<ISyncSource>> selector;

        public SyncSource(params ISyncSource[] sources) : this(x => sources) { }
        public SyncSource(Func<Type, ISyncSource> selector) : this(x => new[] { selector(x) }) { }
        public SyncSource(Func<Type, IEnumerable<ISyncSource>> selector)
        {
            if (selector == null) throw new ArgumentNullException("selector");

            this.selector = selector;
            this.sync = SynchronizationContext.Current;

            if (sync == null)
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
            var sources = selector(typeof(T));
            if (sources == null || !sources.Any()) return null;
            return new Disposable(sources.Select(action));
        }

        private Action<Delta<T>> RunOnSynchronizationContext<T>(Action<Delta<T>> action)
        {
            return new Action<Delta<T>>(x =>
            {
                if (sync == null)
                {
                    action(x);
                }
                else
                {
                    sync.Post(y => action(x), null);
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

            private readonly Action<Delta<T>> action;
            private readonly SyncSource<T> owner;

            public Subscription(string key, SyncSource<T> owner, Action<Delta<T>> action)
            {
                this.Key = key;
                this.owner = owner;
                this.action = action;
            }

            public void Notify(Delta<T> change)
            {
                action(change);
            }

            public void Dispose()
            {
                owner.Unsubscribe(this);
            }
        }

        private bool notifying;
        private readonly object sync = new object();
        private readonly Dictionary<string, List<Subscription>> keyedSubscriptions = new Dictionary<string, List<Subscription>>();
        private readonly List<Subscription> subscriptions = new List<Subscription>();
        private readonly List<Subscription> abandonedSubscriptions = new List<Subscription>();

        public virtual IDisposable On(Action<Delta<T>> action)
        {
            if (action == null) throw new ArgumentNullException("action");

            lock (sync)
            {
                var result = new Subscription(null, this, action);
                subscriptions.Add(result);
                return result;
            }
        }

        public virtual IDisposable On(string key, Action<Delta<T>> action)
        {
            if (action == null) throw new ArgumentNullException("action");
            if (key == null) throw new ArgumentNullException("key");

            lock (sync)
            {
                List<Subscription> subscriptions;
                var result = new Subscription(key, this, action);
                if (!keyedSubscriptions.TryGetValue(key, out subscriptions))
                {
                    keyedSubscriptions.Add(key, subscriptions = new List<Subscription>());
                }
                subscriptions.Add(result);
                return result;
            }
        }

        private void Unsubscribe(Subscription subscription)
        {
            lock (sync)
            {
                if (notifying)
                {
                    abandonedSubscriptions.Add(subscription);
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
                if (keyedSubscriptions.TryGetValue(subscription.Key, out value))
                {
                    value.Remove(subscription);
                }
            }
            else
            {
                subscriptions.Remove(subscription);
            }
        }

        protected virtual void Notify(Delta<T> change)
        {
            lock (sync)
            {
                for (var i = 0; i < abandonedSubscriptions.Count; i++)
                {
                    Remove(abandonedSubscriptions[i]);
                }

                try
                {
                    notifying = true;

                    NotifyByKey(change);
                    NotifyAll(change);
                }
                finally
                {
                    notifying = false;
                }
            }
        }

        private void NotifyByKey(Delta<T> change)
        {
            if (keyedSubscriptions.Count <= 0) return;

            List<Subscription> subscriptions;
            if (!keyedSubscriptions.TryGetValue(change.Key, out subscriptions)) return;

            for (var i = 0; i < subscriptions.Count; i++)
            {
                subscriptions[i].Notify(change);
            }
        }

        private void NotifyAll(Delta<T> change)
        {
            for (var i = 0; i < subscriptions.Count; i++)
            {
                subscriptions[i].Notify(change);
            }
        }
    }
}
