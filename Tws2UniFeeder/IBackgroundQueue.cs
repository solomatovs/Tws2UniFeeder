using System.Threading;
using System.Threading.Tasks;

namespace Tws2UniFeeder
{
    public interface IBackground<T>
    {
        void AddItem(T workItem);

        Task<T> DequeueAsync(CancellationToken cancellationToken);
    }
}
