using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace journeyofcode.Threading
{
    /// <summary>
    ///     Represents a <see cref="TaskScheduler"/> which executes code on a dedicated, single thread whose <see cref="ApartmentState"/> can be configured.
    /// </summary>
    /// <remarks>
    ///     You can use this class if you want to perform operations on a non thread-safe library from a multi-threaded environment.
    /// </remarks>
    public sealed class SingleThreadTaskScheduler
        : TaskScheduler, IDisposable
    {
        private readonly Thread _thread;
        private readonly CancellationTokenSource _cancellationToken;
        private readonly BlockingCollection<Task> _tasks;
        private bool _done = false;

        /// <summary>
        ///     Indicates the maximum concurrency level this <see cref="T:System.Threading.Tasks.TaskScheduler"/> is able to support.
        /// </summary>
        /// 
        /// <returns>
        ///     Returns <c>1</c>.
        /// </returns>
        public override int MaximumConcurrencyLevel
        {
            get { return 1; }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="SingleThreadTaskScheduler"/>.
        /// </summary>
        public SingleThreadTaskScheduler()
            : this(new CancellationTokenSource())
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="SingleThreadTaskScheduler"/> passing a cancelation token.
        /// </summary>
        /// <param name="cancellationToken">
        ///     An <see cref="CancellationTokenSource"/> used to cancel all tasks runing in this <see cref="SingleThreadTaskScheduler"/>.
        /// </param>
        public SingleThreadTaskScheduler(CancellationTokenSource cancellationToken)
        {
            this._cancellationToken = cancellationToken;
            this._tasks = new BlockingCollection<Task>();
            this._thread = Thread.CurrentThread;
        }

        /// <summary>
        ///     Waits until all scheduled <see cref="Task"/>s on this <see cref="SingleThreadTaskScheduler"/> have executed and then disposes this <see cref="SingleThreadTaskScheduler"/>.
        /// </summary>
        /// <remarks>
        ///     Calling this method will block execution. It should only be called once.
        /// </remarks>
        /// <exception cref="TaskSchedulerException">
        ///     Thrown when this <see cref="SingleThreadTaskScheduler"/> already has been disposed by calling either <see cref="Wait"/> or <see cref="Dispose"/>.
        /// </exception>
        public void Wait()
        {
            this.VerifyNotCancelled();

            this._tasks.CompleteAdding();
            this.ThreadStart();
        }

        /// <summary>
        ///     Disposes this <see cref="SingleThreadTaskScheduler"/> by not accepting any further work and not executing previously scheduled tasks.
        /// </summary>
        /// <remarks>
        ///     Call <see cref="Wait"/> instead to finish all queued work. You do not need to call <see cref="Dispose"/> after calling <see cref="Wait"/>.
        /// </remarks>
        public void Dispose()
        {
            this._tasks.CompleteAdding();
            this.ThreadCancel();
        }

        protected override void QueueTask(Task task)
        {
            this.VerifyNotCancelled();

            this._tasks.Add(task, this._cancellationToken.Token);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            this.VerifyNotCancelled();

            if (this._thread != Thread.CurrentThread)
                return false;
            if (this._cancellationToken.Token.IsCancellationRequested)
                return false;

            this.TryExecuteTask(task);
            return true;
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            this.VerifyNotCancelled();

            return this._tasks.ToArray();
        }

        private void ThreadStart()
        {
            foreach (var task in this._tasks.GetConsumingEnumerable())
            {
                if (this._cancellationToken.Token.IsCancellationRequested)
                    return;
                this.TryExecuteTask(task);
            }
            _done = true;
        }

        private void ThreadCancel()
        {
            foreach (var task in this._tasks.GetConsumingEnumerable())
            {
            }
            _done = true;
        }

        private void VerifyNotCancelled()
        {
            if (this._cancellationToken.IsCancellationRequested || _done)
                throw new TaskSchedulerException("Operation invalid after being cancelled.");
        }
    }
}