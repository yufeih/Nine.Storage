namespace Nine.Storage
{
    using System;
    using System.ComponentModel;
    using System.Threading;
    using Nine.Formatting;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class SyncSourceExtensions
    {
        class Defaults<T> where T : class, new() { public static readonly T Value = new T(); }

        private static readonly JsonFormatter formatter = new JsonFormatter();
        
        public static IDisposable Sync<T>(this ISyncSource source, string key, Action<T> action) where T : class, IKeyed, new()
        {
            action = PostToSynchronizationContext(action);
            var result = source.On<T>(key, change => action(change.Value ?? Defaults<T>.Value));
            action(Defaults<T>.Value);
            return result;
        }

        public static IDisposable Sync<T>(this ISyncSource source, Action<T> action) where T : class, IKeyed, new()
        {
            action = PostToSynchronizationContext(action);
            var result = source.On<T>(change => action(change.Value ?? Defaults<T>.Value));
            action(Defaults<T>.Value);
            return result;
        }

        public static void Sync<T>(this IStorage storage, Action<T> action) where T : class, IKeyed, new()
        {
            var source = storage as ISyncSource;
            if (source == null) throw new ArgumentException("storage", "storage needs to be a sync source");

            action = PostToSynchronizationContext(action);
            source.On<T>(x => action(x ?? Defaults<T>.Value));
            action(Defaults<T>.Value);
        }

        public static async void Sync<T>(this IStorage storage, string key, Action<T> action) where T : class, IKeyed, new()
        {
            var source = storage as ISyncSource;
            if (source == null) throw new ArgumentException("storage", "storage needs to be a sync source");

            action = PostToSynchronizationContext(action);
            source.On<T>(key, x => action(x ?? Defaults<T>.Value));

            // Populate the initial value
            var value = await storage.Get<T>(key).ConfigureAwait(false);
            if (value != null) action(value);
        }

        public static async void Sync<T>(this IStorage storage, string key, Action<T, T> action) where T : class, IKeyed, new()
        {
            var source = storage as ISyncSource;
            if (source == null) throw new ArgumentException("storage", "storage needs to be a sync source");

            T oldValue = Defaults<T>.Value;

            action = PostToSynchronizationContext(action);
            source.On<T>(key, x =>
            {
                var copy = formatter.Copy(x);
                action(x ?? Defaults<T>.Value, oldValue);
                oldValue = copy;
            });

            // TODO: Add test case
            var value = await storage.Get<T>(key).ConfigureAwait(false);
            action(value ?? Defaults<T>.Value, Defaults<T>.Value);
        }

        public static async void Sync<T>(this IStorage storage, string key, Func<T, object> watch, Action<T> action) where T : class, IKeyed, new()
        {
            var source = storage as ISyncSource;
            if (source == null) throw new ArgumentException("storage", "storage needs to be a sync source");

            T oldValue = Defaults<T>.Value;

            action = PostToSynchronizationContext(action);
            source.On<T>(key, x =>
            {
                var copy = formatter.Copy(x);
                if (watch != null && watch(x ?? Defaults<T>.Value) != watch(oldValue ?? Defaults<T>.Value))
                {
                    action(x ?? Defaults<T>.Value);
                }
                oldValue = copy;
            });

            // TODO: Add test case
            var value = await storage.Get<T>(key).ConfigureAwait(false);
            action(value ?? Defaults<T>.Value);
        }

        private static Action<T> PostToSynchronizationContext<T>(Action<T> action) where T : class, IKeyed, new()
        {
            var syncContext = SynchronizationContext.Current;
            if (syncContext == null) return action;
            return new Action<T>(target => syncContext.Post(x => action(target), null));
        }

        private static Action<T, T> PostToSynchronizationContext<T>(Action<T, T> action) where T : class, IKeyed, new()
        {
            var syncContext = SynchronizationContext.Current;
            if (syncContext == null) return action;
            return new Action<T, T>((a, b) => syncContext.Post(x => action(a, b), null));
        }
    }
}
