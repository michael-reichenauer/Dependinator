using Dependinator.Modeling;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Private.Items;

namespace Dependinator.ModelViewing.Private
{
	internal class NodeService : INodeService
	{
		private readonly INodeViewModelService nodeViewModelService;
		private readonly Model model;


		public NodeService(
			Model model,
			INodeViewModelService nodeViewModelService)
		{
			this.model = model;
			this.nodeViewModelService = nodeViewModelService;
		}

		public void UpdateNode(DataNode dataNode)
		{
			NodeName name = new NodeName(dataNode.Name);

			if (model.TryGetNode(name, out Node existingNode))
			{
				// TODO: Check node properties as well and update if changed
				return;
			}

			NodeName parentName = name.ParentName;
			Node parentNode = GetNode(parentName);

			NodeType nodeType = new NodeType(dataNode.NodeType);
			Node node = new Node(name, nodeType);
			AddNode(node, parentNode);
		}


		private void AddNode(Node node, Node parentNode)
		{		
			model.Add(node);
			parentNode.AddChild(node);

			CreateNodeViewModel(node);

			AddNodeToParentCanvas(node, parentNode);
		}


		private void CreateNodeViewModel(Node node)
		{
			if (node.NodeType == NodeType.Member)
			{
				node.ViewModel = new MemberNodeViewModel(nodeViewModelService, node);
			}
			else if (node.NodeType == NodeType.Type)
			{
				node.ViewModel = new TypeViewModel(nodeViewModelService, node);
				node.ItemsCanvas = GetChildrenCanvas(node);
			}
			else
			{
				node.ViewModel = new NamespaceViewModel(nodeViewModelService, node);
				node.ItemsCanvas = GetChildrenCanvas(node);
			}
		}


		private void AddNodeToParentCanvas(Node node, Node parentNode)
		{
			nodeViewModelService.SetLayout(node.ViewModel);

			ItemsCanvas parentCanvas = parentNode.ItemsCanvas;

			parentCanvas.AddItem(node.ViewModel);
		}


		private Node GetNode(NodeName nodeName)
		{
			if (model.TryGetNode(nodeName, out Node node))
			{
				return node;
			}

			// The node not yet added. We need the parent to add the node
			NodeName parentName = nodeName.ParentName;
			Node parent = GetNode(parentName);
			node = new Node(nodeName, NodeType.NameSpace);
			AddNode(node, parent);
			return node;
		}


		private static ItemsCanvas GetChildrenCanvas(Node node)
		{
			// First trying to get ann previously created items canvas
			if (node.ItemsCanvas != null)
			{
				return node.ItemsCanvas;
			}

			// The node does not yet have a canvas. So we need to get the parent canvas and
			// then create a child canvas for this node.
			ItemsCanvas parentCanvas = GetChildrenCanvas(node.Parent);

			// Creating the child canvas to be the children canvas of the node
			node.ItemsCanvas = parentCanvas.CreateChildCanvas(node.ViewModel);
			node.ViewModel.ItemsViewModel = new ItemsViewModel(node.ItemsCanvas);

			return node.ItemsCanvas;
		}
	}
}