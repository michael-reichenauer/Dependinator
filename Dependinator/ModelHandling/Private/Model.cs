using System.Collections.Generic;
using System.Linq;
using Dependinator.Common;
using Dependinator.ModelHandling.Core;
using Dependinator.ModelHandling.Private.Items;
using Dependinator.Utils;


namespace Dependinator.ModelHandling.Private
{

	[SingleInstance]
	internal class Model
	{
		private readonly Dictionary<NodeName, Node> nodes = new Dictionary<NodeName, Node>();

		private readonly Dictionary<NodeName, Item> queuedNodes = new Dictionary<NodeName, Item>();
	

		public Model()
		{
			AddRoot();
		}


		public Node Root { get; private set; }

		public Node Node(NodeName name) => nodes[name];

		public bool TryGetNode(NodeName name, out Node node) => nodes.TryGetValue(name, out node);

		public void Add(Node node) => nodes[node.Name] = node;

		public void Remove(Node node) => nodes.Remove(node.Name);


		public void RemoveAll()
		{
			ItemsCanvas rootCanvas = Root.ItemsCanvas;
			nodes.Clear();

			AddRoot();
			Root.ItemsCanvas = rootCanvas;
		}


		private void AddRoot()
		{
			Root = new Node(NodeName.Root);
			Root.NodeType = NodeType.NameSpace;

			Add(Root);
		}


		public void QueueModelLink(NodeName targetName, ModelLink modelLink)
		{
			if (!queuedNodes.TryGetValue(targetName, out Item item))
			{
				item = new Item(targetName, modelLink.TargetType);
				queuedNodes[targetName] = item;
			}

			item.Links.Add(modelLink);
		}


		public void QueueModelLine(NodeName targetName, ModelLine modelLine)
		{
			if (!queuedNodes.TryGetValue(targetName, out Item item))
			{
				item = new Item(targetName, modelLine.TargetType);
				queuedNodes[targetName] = item;
			}

			item.Lines.Add(modelLine);
		}


		public IReadOnlyList<ModelNode> GetAllQueuedNodes()
		{
			return queuedNodes.Values
				.DistinctBy(item => item.TargetName)
				.Select(item => new ModelNode(item.TargetName.FullName, null, item.TargetType, null))
				.ToList();
		}



		public bool TryGetQueuedLinesAndLinks(
			NodeName targetName,
			out IReadOnlyList<ModelLine> lines,
			out IReadOnlyList<ModelLink> links)
		{
			if (!queuedNodes.TryGetValue(targetName, out Item item))
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




		private class Item
		{
			public Item(NodeName targetName, NodeType targetType)
			{
				TargetName = targetName;
				TargetType = targetType;
			}

			
			public NodeName TargetName { get; }
			public NodeType TargetType { get; }
			public List<ModelLine> Lines { get; } = new List<ModelLine>();
			public List<ModelLink> Links { get; } = new List<ModelLink>();
		}

	}
}