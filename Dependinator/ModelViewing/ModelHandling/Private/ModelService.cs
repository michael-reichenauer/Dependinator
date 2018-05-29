using System.Collections.Generic;
using System.Linq;
using Dependinator.Common;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;


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

		public IEnumerable<Node> AllNodes => nodes.Values;

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


		public void QueueModelLink(NodeName nodeName, ModelLink modelLink)
		{
			QueuedNode queuedNode = GetQueuedNode(nodeName, modelLink.TargetType);

			queuedNode.Links.Add(modelLink);
		}


		public void QueueModelLine(NodeName nodeName, ModelLine modelLine)
		{
			QueuedNode queuedNode = GetQueuedNode(nodeName, modelLine.TargetType);

			queuedNode.Lines.Add(modelLine);
		}


		public IReadOnlyList<ModelNode> GetAllQueuedNodes()
		{
			return queuedNodes.Values
				.DistinctBy(item => item.Name)
				.Select(item => new ModelNode(item.Name.FullName, null, item.NodeType, null, null))
				.ToList();
		}


		public bool TryGetQueuedLinesAndLinks(
			NodeName targetName,
			out IReadOnlyList<ModelLine> lines,
			out IReadOnlyList<ModelLink> links)
		{
			if (!queuedNodes.TryGetValue(targetName, out QueuedNode item))
			{
				lines = null;
				links = null;
				return false;
			}

			lines = item.Lines;
			links = item.Links;
			return true;
		}


		public void RemovedQueuedNode(NodeName targetName) => queuedNodes.Remove(targetName);


		private QueuedNode GetQueuedNode(NodeName nodeName, NodeType nodeType)
		{
			if (!queuedNodes.TryGetValue(nodeName, out QueuedNode queuedNode))
			{
				queuedNode = new QueuedNode(nodeName, nodeType);
				queuedNodes[nodeName] = queuedNode;
			}

			return queuedNode;
		}


		private class QueuedNode
		{
			public QueuedNode(NodeName name, NodeType nodeType)
			{
				Name = name;
				NodeType = nodeType;
			}


			public NodeName Name { get; }
			public NodeType NodeType { get; }
			public List<ModelLine> Lines { get; } = new List<ModelLine>();
			public List<ModelLink> Links { get; } = new List<ModelLink>();
		}
	}
}