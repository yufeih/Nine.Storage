namespace Nine.Storage.Syncing
{
    using System;
    using System.ComponentModel;
    using System.Threading;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class SyncSourceExtensions
    {
        public static IDisposable On<T>(this ISyncSource source, string key, Action<T> action)
        {
            return source.On<T>(key, change => action(change.Value));
        }

        public static IDisposable On<T>(this ISyncSource source, Action<T> action)
        {
            return source.On<T>(change => action(change.Value));
        }

        public static IDisposable On<T>(this IStorage storage, Action<T> action, bool runOnSyncContext = true)
        {
            var source = storage as ISyncSource;
            if (source == null) throw new ArgumentException("storage", "storage needs to be a sync source");
            if (runOnSyncContext) action = PostToSynchronizationContext(action);
            return source.On<T>(action);
        }

        public static IDisposable On<T>(this IStorage storage, string key, Action<T> action, bool runOnSyncContext = true)
        {
            var source = storage as ISyncSource;
            if (source == null) throw new ArgumentException("storage", "storage needs to be a sync source");
            if (runOnSyncContext) action = PostToSynchronizationContext(action);
            return source.On<T>(key, action);
        }

        public static IDisposable On<T>(this IStorage storage, string key, Action<Delta<T>> action, bool runOnSyncContext = true)
        {
            var source = storage as ISyncSource;
            if (source == null) throw new ArgumentException("storage", "storage needs to be a sync source");
            if (runOnSyncContext) action = PostToSynchronizationContext(action);
            return source.On<T>(key, action);
        }

        public static IDisposable On<T>(this IStorage storage, string key, Action<T, T> action, bool runOnSyncContext = true) where T : class, new()
        {
            var source = storage as ISyncSource;
            if (source == null) throw new ArgumentException("storage", "storage needs to be a sync source");
            if (runOnSyncContext) action = PostToSynchronizationContext(action);

            T oldValue = null;

            return source.On<T>(key, x =>
            {
                var copy = ObjectHelper.MemberwiseClone(x);
                action(x, oldValue);
                oldValue = copy;
            });
        }

        public static IDisposable On<T>(this IStorage storage, string key, Func<T, object> watch, Action<T> action, bool runOnSyncContext = true) where T : class, new()
        {
            var source = storage as ISyncSource;
            if (source == null) throw new ArgumentException("storage", "storage needs to be a sync source");
            if (runOnSyncContext) action = PostToSynchronizationContext(action);

            T oldValue = null;

            return source.On<T>(key, x =>
            {
                var copy = ObjectHelper.MemberwiseClone(x);
                if (watch == null) return;
                if (x != null && oldValue != null && Equals(watch(x), watch(oldValue))) return;
                action(x);
                oldValue = copy;
            });
        }

        private static Action<T> PostToSynchronizationContext<T>(Action<T> action)
        {
            var syncContext = SynchronizationContext.Current;
            if (syncContext == null) return action;
            return new Action<T>(target => syncContext.Post(x =>
            {
                try
                {
                    action(target);
                } catch { }
            }, null));
        }

        private static Action<T, T> PostToSynchronizationContext<T>(Action<T, T> action)
        {
            var syncContext = SynchronizationContext.Current;
            if (syncContext == null) return action;
            return new Action<T, T>((a, b) => syncContext.Post(x =>
            {
                try
                {
                    action(a, b);
                }
                catch { };
            }, null));
        }
    }
}
