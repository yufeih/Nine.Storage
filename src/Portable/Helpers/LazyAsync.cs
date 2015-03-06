namespace System
{
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides support for asynchronous lazy initialization that is similar to System.Lazy
    /// </summary>
    /// <typeparam name="T">Payload type</typeparam>
    [DebuggerStepThrough]
    class LazyAsync<T>
    {
        private readonly object sync = new object();
        private readonly bool retryOnFailure;
        private readonly Func<Task<T>> valueFactory;
        private TaskCompletionSource<T> tcs;

        /// <summary>
        /// Initializes a new instance of the <see cref="LazyAsync{T}" /> class.
        /// </summary>
        /// <param name="valueFactory">A method that initializes the value.</param>
        /// <param name="retryOnFailure">Whether to re-execute the value factory on failure</param>
        public LazyAsync(Func<Task<T>> valueFactory, bool retryOnFailure = false)
        {
            if (valueFactory == null)
            {
                throw new ArgumentNullException("valueFactory");
            }

            this.valueFactory = valueFactory;
            this.retryOnFailure = retryOnFailure;
        }

        /// <summary>
        /// Gets a value indicating whether the value has been created.
        /// </summary>
        public bool TryGetValue(out T value)
        {
            var tcs = this.tcs;
            if (tcs != null && tcs.Task.IsCompleted)
            {
                value = tcs.Task.Result;
                return true;
            }
            value = default(T);
            return false;
        }

        /// <summary>
        /// Gets the value of the instance asynchronously.
        /// </summary>
        /// <returns>A task of the result.</returns>
        public Task<T> GetValueAsync()
        {
            var tcs = this.tcs;

            // All subsequent requests wait on the task completion source initiated by the first request.
            if (tcs != null) return tcs.Task;

            var newTcs = new TaskCompletionSource<T>();
            var oldTcs = Interlocked.CompareExchange(ref this.tcs, newTcs, null);

            // Didn't aquire the lock
            if (oldTcs != null) return oldTcs.Task;

            // The first request initializes the value
            return this.GetValueCoreAsync(newTcs);
        }

        /// <summary>
        /// Invalidates the cached value.
        /// </summary>
        public void Invalidate()
        {
            Interlocked.Exchange(ref this.tcs, null);
        }

        /// <summary>
        /// Gets the value asynchronously.
        /// </summary>
        /// <returns>A task to retrieve the value.</returns>
        private async Task<T> GetValueCoreAsync(TaskCompletionSource<T> tcs)
        {
            try
            {
                var result = await this.valueFactory().ConfigureAwait(false);
                tcs.SetResult(result);
                return result;
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled();
                if (retryOnFailure)
                {
                    Interlocked.Exchange(ref this.tcs, null);
                }
                throw;
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
                if (retryOnFailure)
                {
                    Interlocked.Exchange(ref this.tcs, null);
                }
                throw;
            }
        }
    }
}