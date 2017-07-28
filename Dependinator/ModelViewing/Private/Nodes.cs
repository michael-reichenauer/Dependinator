using System.Collections.Generic;
using Dependinator.Modeling;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Private.Items;

namespace Dependinator.ModelViewing.Private
{
	internal class Nodes
	{
		private readonly Dictionary<NodeId, Node> nodes = new Dictionary<NodeId, Node>();
		private readonly Dictionary<NodeId, NodeViewModel> viewModels = new Dictionary<NodeId, NodeViewModel>();


		private readonly Dictionary<NodeId, List<Node>> nodesChildren =
			new Dictionary<NodeId, List<Node>>();

		private readonly Dictionary<NodeId, IItemsCanvas> canvases = new Dictionary<NodeId, IItemsCanvas>();


		public Nodes()
		{
			Root = new Node(NodeName.Root, NodeType.NameSpace, NodeId.Root);
			nodes[Root.Id] = Root;
		}

		public void SetRootCanvas(IItemsCanvas rootCanvas) => canvases[Root.Id] = rootCanvas;

		public Node Root { get; }
		public Node Node(NodeId nodeId) => nodes[nodeId];
		public bool TryGetNode(NodeId nodeId, out Node node) => nodes.TryGetValue(nodeId, out node);
		public IReadOnlyList<Node> Children(NodeId nodeId) => GetChildren(nodeId);

		public bool TryGetItemsCanvas(NodeId nodeId, out IItemsCanvas itemsCanvas) =>
			canvases.TryGetValue(nodeId, out itemsCanvas);


		public void Add(Node node)
		{
			nodes[node.Id] = node;

			AddNodeToParentChildren(node);
		}

		public void AddViewModel(NodeId nodeId, NodeViewModel viewModel) =>
			viewModels[nodeId] = viewModel;


		public bool TryGetViewModel(NodeId nodeId, out NodeViewModel viewModel) =>
			viewModels.TryGetValue(nodeId, out viewModel);


		public void AddItemsCanvas(NodeId nodeId, IItemsCanvas itemsCanvas) =>
			canvases[nodeId] = itemsCanvas;


		public IEnumerable<Node> GetAncestors(NodeId nodeId)
		{
			Node current = Node(nodeId);

			do
			{
				current = Node(current.ParentId);
				yield return current;
			} while (current != Root);
		}


		public IEnumerable<Node> GetAncestorsAndSelf(NodeId nodeId)
		{
			Node current = Node(nodeId);
			yield return current;

			do
			{
				current = Node(current.ParentId);
				yield return current;
			} while (current != Root);
		}



		private void AddNodeToParentChildren(Node node)
		{
			var parentChildren = GetChildren(node.ParentId);

			parentChildren.Add(node);
		}


		private List<Node> GetChildren(NodeId nodeId)
		{
			if (!nodesChildren.TryGetValue(nodeId, out List<Node> children))
			{
				children = new List<Node>();
				nodesChildren[nodeId] = children;
			}

			return children;
		}
	}
}