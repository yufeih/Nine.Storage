namespace System
{
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;

    [DebuggerStepThrough]
    static class TestExtensionMethods
    {
        public static Task ChangedTo<T>(this IEnumerable<T> collection, params Func<T, bool>[] elementPredicate)
        {
            var predicate = new Func<T[], bool>(array =>
            {
                if (array.Length != elementPredicate.Length) return false;
                for (int i = 0; i < array.Length; i++) if (!array.All(e => elementPredicate[i](e))) return false;
                return true;
            });
            return ChangedTo(collection, predicate);
        }

        public async static Task ChangedTo<T>(this IEnumerable<T> collection, Func<T[], bool> predicate, int timeout = 5000)
        {
            if (predicate(collection.ToArray())) return;

            var collectionChanged = collection as INotifyCollectionChanged;
            if (collectionChanged == null) throw new TimeoutException();

            var tcs = new TaskCompletionSource<int>();
            var handler = (NotifyCollectionChangedEventHandler)null;
            handler = new NotifyCollectionChangedEventHandler((sender, e) =>
            {
                if (predicate(collection.ToArray()))
                {
                    collectionChanged.CollectionChanged -= handler;
                    tcs.TrySetResult(0);
                }
            });
            collectionChanged.CollectionChanged += handler;

            var delay = Task.Delay(timeout);
            if (delay == await Task.WhenAny(delay, tcs.Task))
            {
                collectionChanged.CollectionChanged -= handler;
                throw new TimeoutException();
            }
        }
    }
}