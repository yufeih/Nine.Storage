namespace Nine.Storage
{
    using System;
    using System.ComponentModel;
    using Nine.Formatting;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class SyncSourceExtensions
    {
        class Defaults<T> where T : class, new() { public static readonly T Value = new T(); }

        private static readonly JsonFormatter formatter = new JsonFormatter();
        
        public static void On<T>(this IStorage storage, string key, Action<T, T> action) where T : class, IKeyed, new()
        {
            var source = storage as ISyncSource;
            if (source == null) throw new ArgumentException("storage", "storage needs to be a sync source");

            T oldValue = Defaults<T>.Value;

            source.On<T>(key, x =>
            {
                var copy = formatter.Copy(x);
                action(x ?? Defaults<T>.Value, oldValue);
                oldValue = copy;
            });
        }

        public static async void Sync<T>(this IStorage storage, string key, Action<T, T> action) where T : class, IKeyed, new()
        {
            var source = storage as ISyncSource;
            if (source == null) throw new ArgumentException("storage", "storage needs to be a sync source");

            On(storage, key, action);

            // TODO: Add test case
            var value = await storage.Get<T>(key).ConfigureAwait(false);
            action(value ?? Defaults<T>.Value, Defaults<T>.Value);
        }
    }
}
