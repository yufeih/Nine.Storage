namespace Nine.Storage
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;

    class SyncThrottler : ISyncSource
    {
        private int ms;
        private ISyncSource source;
        private ConcurrentQueue<object> queue = new ConcurrentQueue<object>();

        public SyncThrottler(ISyncSource source, int millisecondsInterval)
        {
            if (millisecondsInterval <= 0) millisecondsInterval = 33; // 30 FPS

            this.source = source;
            this.ms = millisecondsInterval;

            Tick();
        }

        private async void Tick()
        {
            while (true)
            {
                await Task.Delay(ms);

                // TODO:
            }
        }

        public IDisposable On<T>(Action<Delta<T>> action) where T : class, IKeyed, new()
        {
            return source.On(Throttle(action));
        }

        public IDisposable On<T>(string key, Action<Delta<T>> action) where T : class, IKeyed, new()
        {
            return source.On(key, Throttle(action));
        }

        private Action<Delta<T>> Throttle<T>(Action<Delta<T>> action) where T : class, IKeyed, new()
        {
            throw new NotImplementedException();
        }
    }
}
