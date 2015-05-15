namespace System.Threading
{
    using System.Collections.Concurrent;
    using System.Diagnostics;

    [DebuggerStepThrough]
    class UISynchronizationContext : SynchronizationContext, IDisposable
    {
        struct WorkItem
        {
            public SendOrPostCallback callback;
            public object state;
            public AutoResetEvent handle;
        }

        public static UISynchronizationContext BindToCurrent()
        {
            var result = new UISynchronizationContext();
            SetSynchronizationContext(result);
            return result;
        }

        private bool disposed;
        private Thread uiThread;
        private SynchronizationContext previous = SynchronizationContext.Current;
        private readonly BlockingCollection<WorkItem> workItems = new BlockingCollection<WorkItem>();

        private void EnsureUIThread()
        {
            if (uiThread == null)
            {
                lock (workItems)
                {
                    if (uiThread == null)
                    {
                        uiThread = new Thread(Loop) { Name = "UIThread" };
                        uiThread.Start();
                    }
                }
            }
        }

        private void Loop(object obj)
        {
            SetSynchronizationContext(this);

            while (!disposed)
            {
                var currentItem = workItems.Take();

                try
                {
                    currentItem.callback(currentItem.state);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
                finally
                {
                    if (currentItem.handle != null)
                    {
                        currentItem.handle.Set();
                    }
                }
            }
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            EnsureUIThread();
            workItems.Add(new WorkItem { callback = d, state = state });
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            if (Thread.CurrentThread == uiThread)
            {
                d(state);
            }
            else
            {
                EnsureUIThread();
                var workItem = new WorkItem { callback = d, state = state, handle = new AutoResetEvent(false) };
                workItems.Add(workItem);
                workItem.handle.WaitOne();
            }
        }

        public void Dispose()
        {
            disposed = true;
            SetSynchronizationContext(previous);
        }
    }
}