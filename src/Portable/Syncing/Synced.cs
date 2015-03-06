namespace Nine.Storage
{
    using System;
    using System.Threading.Tasks;

    public class Synced<T> where T : class, IKeyed, new()
    {
        public static readonly T Default = new T();

        private ISyncSource source;
        private string key;

        public T Value { get; private set; }

        public Synced(ISyncSource source) : this(null, source) { }
        public Synced(string key, ISyncSource source)
        {
            this.key = key;
            this.source = source;
            this.Value = Default;

            Listen(x => Value = x);
        }
        
        public async Task ChangedTo(Func<T, bool> predicate, int timeout = 5000)
        {
            if (predicate(Value)) return;

            var tcs = new TaskCompletionSource<int>();
            var action = new Action<T>(value =>
            {
                if (predicate(value)) tcs.TrySetResult(0);
            });
            using (Listen(action))
            {
                var delay = Task.Delay(timeout);
                if (delay == await Task.WhenAny(delay, tcs.Task))
                {
                    throw new TimeoutException();
                }
            }
        }

        private IDisposable Listen(Action<T> action)
        {
            if (key == null)
            {
                return source.On(action);
            }
            else
            {
                return source.On(key, action);
            }
        }
    }
}
