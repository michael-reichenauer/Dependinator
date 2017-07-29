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
			NodeId nodeId = new NodeId(name);
			if (model.Nodes.TryGetNode(nodeId, out Node existingNode))
			{
				// TODO: Check node properties as well and update if changed
				return;
			}

			Node parentNode = GetParentNodeFor(name);

			IItemsCanvas parentCanvas = GetCanvas(parentNode);

			Node node = new Node(name, new NodeType(dataNode.NodeType));
			parentNode.AddChild(node);
			model.Nodes.Add(node);

			AddNode(node, parentCanvas);
		}


		private Node GetParentNodeFor(NodeName nodeName)
		{
			NodeName parentName = nodeName.ParentName;
			NodeId parentId = new NodeId(parentName);

			if (model.Nodes.TryGetNode(parentId, out Node parent))
			{
				return parent;
			}

			// The parent node not yet added, but we need the grand parent to have a parent for th parent
			Node grandParent = GetParentNodeFor(parentName);

			parent = new Node(parentName, NodeType.NameSpace);
			grandParent.AddChild(parent);
			model.Nodes.Add(parent);

			return parent;
		}


		private IItemsCanvas GetCanvas(Node node)
		{
			// First trying to get ann previously created items canvas
			if (node.ChildrenCanvas != null)
			{
				return node.ChildrenCanvas;
			}

			// The node does not yet have a canvas. So we need to get the parent canvas and
			// then create a child canvas for this node. But since the parent might also not yet
			// have a canvas, we traverse the ancestors of the node and create all the needed
			// canvases.
			IItemsCanvas parentCanvas = GetCanvas(node.Parent);
			IItemsCanvas itemsCanvas = AddCompositeNode(node, parentCanvas);

			return itemsCanvas;
		}


		private IItemsCanvas AddCompositeNode(Node node, IItemsCanvas parentCanvas)
		{
			NodeViewModel viewModel = node.ViewModel;
			if (viewModel == null)
			{
				viewModel = AddNode(node, parentCanvas);
			}

			IItemsCanvas itemsCanvas = parentCanvas.CreateChild(viewModel);

			if (viewModel is CompositeNodeViewModel composite)
			{
				composite.ItemsViewModel = new ItemsViewModel(itemsCanvas);
			}

			node.ChildrenCanvas = itemsCanvas;
			return itemsCanvas;
		}


		private NodeViewModel AddNode(Node node, IItemsCanvas parentCanvas)
		{
			NodeViewModel nodeViewModel = CreateNodeViewModel(node);
			nodeViewModelService.SetLayout(nodeViewModel);
			node.ViewModel = nodeViewModel;

			parentCanvas.AddItem(nodeViewModel);

			return nodeViewModel;
		}


		private NodeViewModel CreateNodeViewModel(Node node)
		{
			NodeViewModel nodeViewModel;
			if (node.NodeType == NodeType.Type)
			{
				nodeViewModel = new TypeViewModel(nodeViewModelService, node);
			}
			else if (node.NodeType == NodeType.Member)
			{
				nodeViewModel = new MemberNodeViewModel(nodeViewModelService, node);
			}
			else
			{
				nodeViewModel = new NamespaceViewModel(nodeViewModelService, node);
			}

			return nodeViewModel;
		}
	}
}