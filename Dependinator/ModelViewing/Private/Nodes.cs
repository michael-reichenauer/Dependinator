using System.Collections.Generic;
using Dependinator.ModelViewing.Nodes;

namespace Dependinator.ModelViewing.Private
{
	internal class Nodes
	{
		private readonly Dictionary<NodeId, Node> nodes = new Dictionary<NodeId, Node>();
		private readonly Dictionary<NodeId, NodeViewModel> viewModels = new Dictionary<NodeId, NodeViewModel>();


		public Nodes()
		{
			Root = new Node(NodeName.Root, NodeType.NameSpace);
			nodes[Root.Id] = Root;
		}


		public Node Root { get; }
		public Node Node(NodeId nodeId) => nodes[nodeId];
		public bool TryGetNode(NodeId nodeId, out Node node) => nodes.TryGetValue(nodeId, out node);


		public void Add(Node node)
		{
			nodes[node.Id] = node;
		}

		public void AddViewModel(NodeId nodeId, NodeViewModel viewModel) =>
			viewModels[nodeId] = viewModel;


		public bool TryGetViewModel(NodeId nodeId, out NodeViewModel viewModel) =>
			viewModels.TryGetValue(nodeId, out viewModel);
	}
}