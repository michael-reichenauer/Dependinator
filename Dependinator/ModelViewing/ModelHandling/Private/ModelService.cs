using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils.Dependencies;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	[SingleInstance]
	internal class ModelService : IModelService
	{
		private readonly Dictionary<NodeName, Node> nodes = new Dictionary<NodeName, Node>();

		private readonly Dictionary<NodeName, QueuedNode> queuedNodes = new Dictionary<NodeName, QueuedNode>();


		public ModelService()
		{
			AddRoot();
		}


		public Node Root { get; private set; }

		public IEnumerable<Node> AllNodes => nodes.Select(pair => pair.Value);

		public Node GetNode(NodeName name) => nodes[name];

		public bool TryGetNode(NodeName name, out Node node) => nodes.TryGetValue(name, out node);


		public void Add(Node node) => nodes[node.Name] = node;

		public void Remove(Node node) => nodes.Remove(node.Name);


		public void RemoveAll()
		{
			ItemsCanvas rootCanvas = Root.View.ItemsCanvas;
			nodes.Clear();

			AddRoot();
			Root.View.ItemsCanvas = rootCanvas;
		}


		private void AddRoot()
		{
			Root = new Node(NodeName.Root);
			Root.NodeType = NodeType.NameSpace;

			Add(Root);
		}


		public void QueueModelLink(NodeName nodeId, DataLink dataLink)
		{
			QueuedNode queuedNode = GetQueuedNode(nodeId);

			queuedNode.Links.Add(dataLink);
		}


		public void QueueModelLine(NodeName nodeId, DataLine dataLine)
		{
			QueuedNode queuedNode = GetQueuedNode(nodeId);

			queuedNode.Lines.Add(dataLine);
		}


		public IReadOnlyList<DataNode> GetAllQueuedNodes()
		{
			return queuedNodes
				.Where(pair => pair.Value.DataNode != null)
				.Select(pair => pair.Value.DataNode)
				.ToList();
		}


		public void QueueNode(DataNode node)
		{
			QueuedNode queuedNode = GetQueuedNode(NodeName.From(node.Name.FullName));

			if (queuedNode.DataNode == null)
			{
				queuedNode.DataNode = new DataNode(
					node.Name,
					node.Parent,
					node.NodeType)
				{
					Description = node.Description
				};
			}
		}


		public bool TryGetQueuedLinesAndLinks(
			NodeName target,
			out IReadOnlyList<DataLine> lines,
			out IReadOnlyList<DataLink> links)
		{
			if (!queuedNodes.TryGetValue(target, out QueuedNode item))
			{
				lines = null;
				links = null;
				return false;
			}

			lines = item.Lines;
			links = item.Links;
			return true;
		}


		public void RemovedQueuedNode(NodeName nodeName) => queuedNodes.Remove(nodeName);


		private QueuedNode GetQueuedNode(NodeName nodeName)
		{
			if (!queuedNodes.TryGetValue(nodeName, out QueuedNode queuedNode))
			{
				queuedNode = new QueuedNode();
				queuedNodes[nodeName] = queuedNode;
			}

			return queuedNode;
		}


		private class QueuedNode
		{
			public DataNode DataNode { get; set; }
			public List<DataLine> Lines { get; } = new List<DataLine>();
			public List<DataLink> Links { get; } = new List<DataLink>();
		}
	}
}