using System.Collections.Concurrent;

namespace Tws2UniFeeder
{
    public class FixedSizedQueue<T> : ConcurrentQueue<T>
    {
        private readonly object syncObject = new object();

        public int Size { get; set; }

        public FixedSizedQueue(int size)
        {
            Size = size;
        }
        public FixedSizedQueue()
        {
            Size = 10;
        }

        public new void Enqueue(T obj)
        {
            base.Enqueue(obj);
            lock (syncObject)
            {
                while (base.Count > Size)
                {
                    base.TryDequeue(out _);
                }
            }
        }
    }
}
