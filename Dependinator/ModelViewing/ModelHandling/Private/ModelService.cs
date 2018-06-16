using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.DataHandling;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils.Dependencies;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	[SingleInstance]
	internal class ModelService : IModelService
	{
		private readonly Dictionary<NodeId, Node> nodes = new Dictionary<NodeId, Node>();

		private readonly Dictionary<NodeId, QueuedNode> queuedNodes = new Dictionary<NodeId, QueuedNode>();


		public ModelService()
		{
			AddRoot();
		}


		public Node Root { get; private set; }

		public IEnumerable<Node> AllNodes => nodes.Values;

		public Node GetNode(NodeId name) => nodes[name];

		public bool TryGetNode(NodeId name, out Node node) => nodes.TryGetValue(name, out node);


		public void Add(Node node) => nodes[node.Id] = node;

		public void Remove(Node node) => nodes.Remove(node.Id);


		public void RemoveAll()
		{
			ItemsCanvas rootCanvas = Root.View.ItemsCanvas;
			nodes.Clear();

			AddRoot();
			Root.View.ItemsCanvas = rootCanvas;
		}


		private void AddRoot()
		{
			Root = new Node(NodeId.Root, NodeName.Root);
			Root.NodeType = NodeType.NameSpace;

			Add(Root);
		}


		public void QueueModelLink(NodeId nodeId, DataLink dataLink)
		{
			QueuedNode queuedNode = GetQueuedNode(nodeId);

			queuedNode.Links.Add(dataLink);
		}


		public void QueueModelLine(NodeId nodeId, DataLine dataLine)
		{
			QueuedNode queuedNode = GetQueuedNode(nodeId);

			queuedNode.Lines.Add(dataLine);
		}


		public IReadOnlyList<DataNode> GetAllQueuedNodes()
		{
			return queuedNodes
				.Where(pair => pair.Value.DataNode != null)
				.Select(pair =>pair.Value.DataNode)
				.ToList();
		}


		public void QueueNode(DataNode node)
		{
			QueuedNode queuedNode = GetQueuedNode(node.Id);
			
			if (queuedNode.DataNode == null)
			{
				queuedNode.DataNode = new DataNode(
					node.Id, 
					node.Name, 
					node.Parent, 
					node.NodeType,
					node.Description, 
					node.CodeText);
			}
		}


		public bool TryGetQueuedLinesAndLinks(
			NodeId targetId,
			out IReadOnlyList<DataLine> lines,
			out IReadOnlyList<DataLink> links)
		{
			if (!queuedNodes.TryGetValue(targetId, out QueuedNode item))
			{
				lines = null;
				links = null;
				return false;
			}

			lines = item.Lines;
			links = item.Links;
			return true;
		}


		public void RemovedQueuedNode(NodeId nodeId) => queuedNodes.Remove(nodeId);


		private QueuedNode GetQueuedNode(NodeId nodeId)
		{
			if (!queuedNodes.TryGetValue(nodeId, out QueuedNode queuedNode))
			{
				queuedNode = new QueuedNode(nodeId);
				queuedNodes[nodeId] = queuedNode;
			}

			return queuedNode;
		}


		private class QueuedNode
		{
			public QueuedNode(NodeId nodeId)
			{
				NodeId = nodeId;
			}


			public NodeId NodeId { get; }
			public DataNode DataNode { get; set; }
			public List<DataLine> Lines { get; } = new List<DataLine>();
			public List<DataLink> Links { get; } = new List<DataLink>();
		}
	}
}