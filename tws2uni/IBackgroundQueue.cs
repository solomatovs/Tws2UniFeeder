using System;
using System.Threading;
using System.Threading.Tasks;

namespace tws2uni
{
    public interface IBackgroundQueue<T>
    {
        void QueueBackgroundWorkItem(T workItem);

        Task<T> DequeueAsync(CancellationToken cancellationToken);

        int CountTask();
    }
}
