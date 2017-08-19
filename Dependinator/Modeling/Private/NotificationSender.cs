using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.Modeling.Private.Serializing;
using Dependinator.Utils;


namespace Dependinator.Modeling.Private
{
	internal class NotificationSender
	{
		private readonly NotificationReceiver receiver;
		private readonly BlockingCollection<object> items = new BlockingCollection<object>();

		private readonly Task sendTask;


		public NotificationSender(NotificationReceiver receiver)
		{
			this.receiver = receiver;

			sendTask = Task.Run(() => Sender());
		}


		public void SendNode(Dtos.Node node) => items.Add(node);

		public void SendLink(Dtos.Link link) => items.Add(link);


		public void Flush()
		{
			items.CompleteAdding();

			// Wait until all notification have been sent
			sendTask.Wait();
		}


		private void Sender()
		{
			try
			{
				while (!items.IsCompleted)
				{
					object item;
					if (!items.TryTake(out item, int.MaxValue))
					{
						return;
					}

					List<Dtos.Node> nodeBatch = null;
					List<Dtos.Link> linkBatch = null;

					AddToBatch(item, ref nodeBatch, ref linkBatch);

					while (items.TryTake(out item))
					{
						AddToBatch(item, ref nodeBatch, ref linkBatch);
					}

					if (nodeBatch?.Any() ?? false)
					{
						receiver.ReceiveNodes(nodeBatch);
					}

					if (linkBatch?.Any() ?? false)
					{
						receiver.ReceiveLinks(linkBatch);
					}
				}
			}
			catch (Exception e)
			{
				Log.Warn($"exception {e}");
			}
		}


		private static void AddToBatch(
			object item,
			ref List<Dtos.Node> nodeBatch,
			ref List<Dtos.Link> linkBatch)
		{
			if (item is Dtos.Node node)
			{
				if (nodeBatch == null)
				{
					nodeBatch = new List<Dtos.Node>();
				}

				nodeBatch.Add(node);
			}
			else if (item is Dtos.Link link)
			{
				if (linkBatch == null)
				{
					linkBatch = new List<Dtos.Link>();
				}

				linkBatch.Add(link);
			}
		}
	}
}