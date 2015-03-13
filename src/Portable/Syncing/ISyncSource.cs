namespace Nine.Storage
{
    using System;
    using System.ComponentModel;

    public enum DeltaAction
    {
        Add,
        Put,
        Remove,
    }

    public struct Delta<T>
    {
        public DeltaAction Action;
        public string Key;
        public T Value;

        public Delta(DeltaAction action, string key, T value = default(T))
        {
            this.Action = action;
            this.Key = key;
            this.Value = value;
        }

        public bool TryMerge(ref Delta<T> delta)
        {
            if (delta.Key != Key) return false;
            if (Action == DeltaAction.Remove && delta.Action == DeltaAction.Remove) return true;

            Action = DeltaAction.Put;
            Value = delta.Value;
            return true;
        }
    }

    /// <summary>
    /// Enables change notification
    /// </summary>
    public interface ISyncSource<T> where T : class, IKeyed, new()
    {
        IDisposable On(Action<Delta<T>> action);
        IDisposable On(string key, Action<Delta<T>> action);
    }

    /// <summary>
    /// Enables change notification
    /// </summary>
    public interface ISyncSource
    {
        IDisposable On<T>(Action<Delta<T>> action) where T : class, IKeyed, new();
        IDisposable On<T>(string key, Action<Delta<T>> action) where T : class, IKeyed, new();
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class SyncSourceExtensions
    {
        class Defaults<T> where T : class, new() { public static readonly T Value = new T(); }

        public static IDisposable On<T>(this ISyncSource source, string minKey, string maxKey, Action<Delta<T>> action) where T : class, IKeyed, new()
        {
            return source.On<T>(change => HandleDelta(change, minKey, maxKey, action));
        }
        
        public static IDisposable On<T>(this ISyncSource source, string key, Action<T> action) where T : class, IKeyed, new()
        {
            return source.On<T>(key, change => action(change.Value ?? Defaults<T>.Value));
        }

        public static IDisposable Sync<T>(this ISyncSource source, string key, Action<T> action) where T : class, IKeyed, new()
        {
            var result = source.On<T>(key, change => action(change.Value ?? Defaults<T>.Value));
            action(Defaults<T>.Value);
            return result;
        }

        public static IDisposable On<T>(this ISyncSource source, Action<T> action) where T : class, IKeyed, new()
        {
            return source.On<T>(change => action(change.Value ?? Defaults<T>.Value));
        }

        public static IDisposable Sync<T>(this ISyncSource source, Action<T> action) where T : class, IKeyed, new()
        {
            var result = source.On<T>(change => action(change.Value ?? Defaults<T>.Value));
            action(Defaults<T>.Value);
            return result;
        }

        public static void On<T>(this IStorage storage, Action<T> action) where T : class, IKeyed, new()
        {
            var source = storage as ISyncSource;
            if (source == null) throw new ArgumentException("storage", "storage needs to be a sync source");
            
            source.On<T>(x => action(x ?? Defaults<T>.Value));
        }

        public static void Sync<T>(this IStorage storage, Action<T> action) where T : class, IKeyed, new()
        {
            var source = storage as ISyncSource;
            if (source == null) throw new ArgumentException("storage", "storage needs to be a sync source");

            source.On<T>(x => action(x ?? Defaults<T>.Value));
            action(Defaults<T>.Value);
        }

        public static void On<T>(this IStorage storage, string key, Action<T> action) where T : class, IKeyed, new()
        {
            var source = storage as ISyncSource;
            if (source == null) throw new ArgumentException("storage", "storage needs to be a sync source");

            source.On<T>(key, x => action(x ?? Defaults<T>.Value));
        }

        public static async void Sync<T>(this IStorage storage, string key, Action<T> action) where T : class, IKeyed, new()
        {
            var source = storage as ISyncSource;
            if (source == null) throw new ArgumentException("storage", "storage needs to be a sync source");

            source.On<T>(key, x => action(x ?? Defaults<T>.Value));

            // Populate the initial value
            var value = await storage.Get<T>(key).ConfigureAwait(false);
            if (value != null) action(value);
        }

        public static ISyncSource Throttle(this ISyncSource source, int millisecondsInterval = 0)
        {
            return new SyncThrottler(source, millisecondsInterval);
        }

        private static void HandleDelta<T>(Delta<T> change, string minKey, string maxKey, Action<Delta<T>> action)
        {
            if ((minKey != null && string.CompareOrdinal(change.Key, minKey) < 0) ||
                (maxKey != null && string.CompareOrdinal(change.Key, maxKey) >= 0))
            {
                return;
            }

            action(change);
        }
    }
}
