using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace tws2uni
{
    public class BackgroundTaskQueue : IBackgroundQueue<Func<CancellationToken, Task>>
    {
        private readonly ConcurrentQueue<Func<CancellationToken, Task>> workItems = new ConcurrentQueue<Func<CancellationToken, Task>>();
        private readonly SemaphoreSlim signal = new SemaphoreSlim(0);

        public void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            workItems.Enqueue(workItem);
            signal.Release();
        }

        public async Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
        {
            await signal.WaitAsync(cancellationToken);
            workItems.TryDequeue(out var workItem);

            return workItem;
        }

        public int CountTask()
        {
            return workItems.Count;
        }
    }
}
