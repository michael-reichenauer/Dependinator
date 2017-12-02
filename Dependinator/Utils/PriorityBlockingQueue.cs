using System.Collections.Concurrent;
using System.Linq;


namespace Dependinator.Utils
{
	public class PriorityBlockingQueue<T>
	{
		private readonly BlockingCollection<T>[] queues;


		public PriorityBlockingQueue(int priorityCount)
		{
			queues = new BlockingCollection<T>[priorityCount];

			for (int i = 0; i < queues.Length; i++)
			{
				queues[i] = new BlockingCollection<T>(new ConcurrentQueue<T>());
			}
		}


		public void Enqueue(T item, int priority)
		{
			queues[priority].Add(item);
		}


		public bool TryTake(out T item, int timeout)
		{
			item = default(T);

			return -1 != BlockingCollection<T>.TryTakeFromAny(queues, out item, timeout);
		}


		public void CompleteAdding() => queues.ForEach(queue => queue.CompleteAdding());
	}
}