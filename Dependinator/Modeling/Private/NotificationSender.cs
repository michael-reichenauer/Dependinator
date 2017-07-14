using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.Modeling.Private.Serializing;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;


namespace Dependinator.Modeling.Private
{
	internal class NotificationSender
	{
		private readonly NotificationReceiver receiver;
		private readonly BlockingCollection<Data.Node> nodes = new BlockingCollection<Data.Node>();
		private readonly BlockingCollection<Data.Link> links = new BlockingCollection<Data.Link>();

		private readonly Dictionary<string, Data.Node> sentNodes = new Dictionary<string, Data.Node>();
		private readonly Task nodeTask;
		private readonly Task linkTask;


		public NotificationSender(NotificationReceiver receiver)
		{
			this.receiver = receiver;

			nodeTask = Task.Run(() => NodeSender());
			linkTask = Task.Run(() => LinkSender());
		}



		public Data.Node SendNode(string nodeName, NodeType nodeType)
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
			nodes.Add(node);

			return node;
		}


		public void SendLink(string sourceNodeName, string targetNodeName)
		{
			if (targetNodeName.Contains("&"))
			{
			}

			Data.Link link = new Data.Link
			{
				Source = sourceNodeName,
				Target = targetNodeName
			};

			links.Add(link);
		}


		public void Flush()
		{
			nodes.CompleteAdding();
			links.CompleteAdding();

			// Wait until all notification have been sent
			nodeTask.Wait();
			linkTask.Wait();
		}


		private void NodeSender()
		{
			try
			{
				while (!nodes.IsCompleted)
				{
					Data.Node node;
					if (!nodes.TryTake(out node, int.MaxValue))
					{
						return;
					}
					List<Data.Node> batch = new List<Data.Node>();
					batch.Add(node);

					while (nodes.TryTake(out node))
					{
						batch.Add(node);
					}

					receiver.ReceiveNodes(batch);
				}
			}
			catch (Exception e)
			{
				Log.Warn($"exception {e}");
			}
		}


		private void LinkSender()
		{
			try
			{
				while (!links.IsCompleted)
				{
					Data.Link link;
					if (!links.TryTake(out link, int.MaxValue))
					{
						return;
					}

					List<Data.Link> batch = new List<Data.Link>();
					batch.Add(link);

					while (links.TryTake(out link))
					{
						batch.Add(link);
					}

					receiver.ReceiveLinks(batch);
				}
			}
			catch (Exception e)
			{
				Log.Warn($"exception {e}");
			}
		}
	}
}