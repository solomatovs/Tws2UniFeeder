using System.Threading;
using System.Threading.Tasks;

namespace Tws2UniFeeder
{
    public interface IBackgroundQueue<T>
    {
        void QueueBackgroundWorkItem(T workItem);

        Task<T> DequeueAsync(CancellationToken cancellationToken);

        int CountTask();
    }
}
