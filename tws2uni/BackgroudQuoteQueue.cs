using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Tws2UniFeeder
{
    public class BackgroundQuoteQueue : IBackgroundQueue<Quote>
    {
        private readonly ConcurrentQueue<Quote> workItems = new ConcurrentQueue<Quote>();
        private readonly SemaphoreSlim signal = new SemaphoreSlim(0);

        public void QueueBackgroundWorkItem(Quote workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            workItems.Enqueue(workItem);
            signal.Release();
        }

        public async Task<Quote> DequeueAsync(CancellationToken cancellationToken)
        {
            await signal.WaitAsync(cancellationToken);
            workItems.TryDequeue(out var workItem);

            return workItem;
        }
    }
}
