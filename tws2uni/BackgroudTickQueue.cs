using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace tws2uni
{
    using tws;
    public class BackgroundTickQueue : IBackgroundQueue<TwsTick>
    {
        private readonly ConcurrentQueue<TwsTick> workItems = new ConcurrentQueue<TwsTick>();
        private readonly SemaphoreSlim signal = new SemaphoreSlim(0);

        public void QueueBackgroundWorkItem(TwsTick workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            workItems.Enqueue(workItem);
            signal.Release();
        }

        public async Task<TwsTick> DequeueAsync(CancellationToken cancellationToken)
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
