using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Tws2UniFeeder
{
    public class BackgroundQueue<T> : IBackgroundQueue<T>
    {
        private readonly ConcurrentQueue<T> workItems = new ConcurrentQueue<T>();
        private readonly SemaphoreSlim signal = new SemaphoreSlim(0);

        public void QueueBackgroundWorkItem(T workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            workItems.Enqueue(workItem);
            signal.Release();
        }

        public async Task<T> DequeueAsync(CancellationToken cancellationToken)
        {
            await signal.WaitAsync(cancellationToken);
            workItems.TryDequeue(out var workItem);

            return workItem;
        }
    }
}
