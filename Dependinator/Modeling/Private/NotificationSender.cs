using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.Modeling.Private.Serializing;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;


namespace Dependinator.Modeling.Private
{
	internal class NotificationSender
	{
		private readonly NotificationReceiver receiver;
		private readonly BlockingCollection<object> items = new BlockingCollection<object>();

		private readonly Dictionary<string, Data.Node> sentNodes = new Dictionary<string, Data.Node>();
		private readonly Task sendTask;


		public NotificationSender(NotificationReceiver receiver)
		{
			this.receiver = receiver;

			sendTask = Task.Run(() => Sender());
		}


		public Data.Node SendNode(string nodeName, string nodeType)
		{
			if (sentNodes.TryGetValue(nodeName, out Data.Node node))
			{
				// Already sent this node
				return node;
			}

			node = new Data.Node
			{
				Name = nodeName,
				Type = nodeType
			};

			sentNodes[nodeName] = node;
			items.Add(node);

			return node;
		}


		public void SendLink(string sourceNodeName, string targetNodeName)
		{
			Data.Link link = new Data.Link
			{
				Source = sourceNodeName,
				Target = targetNodeName
			};

			items.Add(link);
		}


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

					List<Data.Node> nodeBatch = null;
					List<Data.Link> linkBatch = null;

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
			ref List<Data.Node> nodeBatch, 
			ref List<Data.Link> linkBatch)
		{
			if (item is Data.Node node)
			{
				if (nodeBatch == null)
				{
					nodeBatch = new List<Data.Node>();
				}

				nodeBatch.Add(node);
			}
			else if (item is Data.Link link)
			{
				if (linkBatch == null)
				{
					linkBatch = new List<Data.Link>();
				}

				linkBatch.Add(link);
			}
		}
	}
}