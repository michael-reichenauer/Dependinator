using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.ModelParsing;
using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Private
{
	internal class ModelNodeService : IModelNodeService
	{
		private readonly INodeViewModelService nodeViewModelService;
		private readonly IModelLinkService modelLinkService;
		private readonly Model model;


		public ModelNodeService(
			Model model,
			IModelLinkService modelLinkService,
			INodeViewModelService nodeViewModelService)
		{
			this.model = model;
			this.modelLinkService = modelLinkService;
			this.nodeViewModelService = nodeViewModelService;
		}

		public void UpdateNode(ModelNode modelNode, int stamp)
		{
			NodeName name = NodeName.From(modelNode.Name);

			if (model.TryGetNode(name, out Node existingNode))
			{
				// TODO: Check node properties as well and update if changed
				existingNode.Stamp = stamp;

				//MoveNodeIfNeeded(existingNode, modelNode.RootGroup);

				if (existingNode.NodeType.AsString() != modelNode.NodeType)
				{
					existingNode.NodeType = new NodeType(modelNode.NodeType);
					UpdateNodeType(existingNode);
				}

				return;
			}
			
			Node parentNode = GetParentNode(name, modelNode);

			AddNode(name, modelNode, parentNode, stamp);
		}


		private void AddNode(NodeName name, ModelNode modelNode, Node parentNode, int stamp)
		{
			Node node = new Node(name)
			{
				Stamp = stamp,
				NodeType = new NodeType(modelNode.NodeType),
				Bounds = modelNode.Bounds,
				ScaleFactor = modelNode.ItemsScaleFactor,
				Offset = modelNode.ItemsOffset,
				Color = modelNode.Color,
				Group = modelNode.Group
			};

			AddNode(node, parentNode);
		}


		public void RemoveAllNodesAndLinks()
		{
			Timing t = Timing.Start();
			IReadOnlyList<Node> nodes = model.Root.Descendents().ToList();

			foreach (Node node in nodes)
			{
				if (node.NodeType != NodeType.NameSpace)
				{
					List<Link> obsoleteLinks = node.SourceLinks.ToList();
					modelLinkService.RemoveObsoleteLinks(obsoleteLinks);

					RemoveNode(node);
				}
				else
				{
					List<Link> obsoleteLinks = node.SourceLinks.ToList();
					modelLinkService.RemoveObsoleteLinks(obsoleteLinks);
				}
			}

			foreach (Node node in nodes.Reverse())
			{
				if (node.NodeType == NodeType.NameSpace && !node.Children.Any())
				{
					// Node is an empty namespace, lets remove it
					RemoveNode(node);
				}
			}

			t.Log($"{nodes.Count} nodes");
		}



		public void RemoveObsoleteNodesAndLinks(int stamp)
		{
			Timing t = Timing.Start();
			IReadOnlyList<Node> nodes = model.Root.Descendents().ToList();

			foreach (Node node in nodes)
			{
				if (node.Stamp != stamp && node.NodeType != NodeType.NameSpace)
				{
					List<Link> obsoleteLinks = node.SourceLinks.ToList();
					modelLinkService.RemoveObsoleteLinks(obsoleteLinks);

					RemoveNode(node);
				}
				else
				{
					List<Link> obsoleteLinks = node.SourceLinks.Where(link => link.Stamp != stamp).ToList();
					modelLinkService.RemoveObsoleteLinks(obsoleteLinks);
				}
			}

			foreach (Node node in nodes.Reverse())
			{
				if (node.NodeType == NodeType.NameSpace && !node.Children.Any())
				{
					// Node is an empty namespace, lets remove it
					RemoveNode(node);
				}
			}

			t.Log($"{nodes.Count} nodes");
		}


		public void RemoveAll()
		{
			model.Root?.ItemsCanvas?.RemoveAll();

			model.RemoveAll();
		}


		public void ResetLayout()
		{
			Timing t = Timing.Start();
			IReadOnlyList<Node> nodes = model.Root.Descendents().ToList();

			foreach (Node node in nodes)
			{
				nodeViewModelService.ResetLayout(node.ViewModel);

				List<Link> links = node.SourceLinks;
				modelLinkService.ResetLayout(links);
			}

			t.Log($"{nodes.Count} nodes");
		}


		private void AddNode(Node node, Node parentNode)
		{
			model.Add(node);
			parentNode.AddChild(node);

			CreateNodeViewModel(node);

			AddNodeToParentCanvas(node, parentNode);
		}


		private void RemoveNode(Node node)
		{
			model.Remove(node);
			node.Parent?.RemoveChild(node);

			if (node.ItemsCanvas != null)
			{
				node.Parent?.ItemsCanvas.RemoveChildCanvas(node.ItemsCanvas);

			}

			RemoveNodeFromParentCanvas(node);
		}


		//private void MoveNode(Node node, Node parentNode)
		//{
		//	node.Parent?.RemoveChild(node);

		//	if (node.ItemsCanvas != null)
		//	{
		//		node.Parent?.ItemsCanvas.RemoveChildCanvas(node.ItemsCanvas);
		//	}

		//	RemoveNodeFromParentCanvas(node);

		//	parentNode.AddChild(node);
		//	ItemsCanvas parentCanvas = GetChildrenCanvas(parentNode);
		//	if (node.ItemsCanvas != null)
		//	{
		//		parentCanvas.AddChildCanvas(node.ItemsCanvas);
		//	}

		//	parentCanvas.AddItem(node.ViewModel);
		//}

		private void UpdateNodeType(Node node)
		{
			RemoveNodeFromParentCanvas(node);
			CreateNodeViewModel(node);
			AddNodeToParentCanvas(node, node.Parent);
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


		private static void RemoveNodeFromParentCanvas(Node node)
		{
			node.Parent?.ItemsCanvas?.RemoveItem(node.ViewModel);
		}


		private Node GetParentNode(NodeName nodeName, ModelNode modelNode)
		{
			NodeName parentName = GetParentName(nodeName, modelNode);

			if (model.TryGetNode(parentName, out var parent))
			{
				return parent;
			}

			Stack<NodeName> ancestorNames = new Stack<NodeName>();
			ancestorNames.Push(parentName);

			while (true)
			{
				parentName = ancestorNames.Peek().ParentName;
				if (model.TryGetNode(parentName, out parent))
				{
					break;
				}

				ancestorNames.Push(parentName);
			}
			
			while (ancestorNames.Any())
			{
				Node grandParent = parent;

				parentName = ancestorNames.Pop();
				
				parent = new Node(parentName);
				parent.NodeType = NodeType.NameSpace;

				AddNode(parent, grandParent);
			}
			
			return parent;
		}


		//private Node GetParentNode(NodeName parentName)
		//{
		//	if (model.TryGetNode(parentName, out var parent))
		//	{
		//		return parent;
		//	}

		//	// The parent not yet added. We need the grandparent to add parent
		//	NodeName grandParentName = parentName.ParentName;
		//	Node grandParent = GetParentNode(grandParentName, rootGroup);

		//	parent = new Node(parentName);
		//	parent.NodeType = NodeType.NameSpace;
		//	parent.RootGroup = rootGroup;

		//	AddNode(parent, grandParent);
		//	return parent;
		//}


		private static NodeName GetParentName(NodeName nodeName, ModelNode modelNode)
		{
			NodeName parentName = nodeName.ParentName;
			string group = modelNode.Group;

			if (group == null)
			{
				return parentName;
			}
			else
			{
				if (parentName == NodeName.Root)
				{
					return NodeName.From(group);
				}
				else
				{
					return NodeName.From($"{parentName}.{group}");
				}
			}
		}
		




		//private Node GetParentNode_org(NodeName parentName, string rootGroup)
		//{
		//	if (parentName == NodeName.Root && rootGroup != null)
		//	{
		//		NodeName name = NodeName.From(rootGroup);

		//		return GetRootGroupNode(name);
		//	}

		//	if (model.TryGetNode(parentName, out var parent))
		//	{
		//		return parent;
		//	}

		//	// The parent not yet added. We need the grandparent to add parent
		//	NodeName grandParentName = parentName.ParentName;
		//	Node grandParent = GetParentNode(grandParentName, rootGroup);
			
		//	parent = new Node(parentName);
		//	parent.NodeType = NodeType.NameSpace;
		//	parent.RootGroup = rootGroup;

		//	AddNode(parent, grandParent);
		//	return parent;
		//}

		

		//private Node GetRootGroupNode_org(NodeName nodeName)
		//{
		//	if (model.TryGetNode(nodeName, out Node node))
		//	{
		//		return node;
		//	}

		//	NodeName parentName = nodeName.ParentName;
		//	Node parent = GetRootGroupNode(parentName);

		//	node = new Node(nodeName);
		//	node.NodeType = NodeType.NameSpace;

		//	AddNode(node, parent);
		//	return node;
		//}


		//private Node GetParentNode_Org(NodeName parentName, string rootGroup)
		//{
		//	Node parent;

		//	if (parentName == NodeName.Root && rootGroup != null)
		//	{
		//		parentName = NodeName.From(rootGroup);
		//		return GetParentNode(parentName, null);
		//	}

		//	if (model.TryGetNode(parentName, out parent))
		//	{

		//		return parent;
		//	}

		//	// The parent not yet added. We need the grandparent to add parent
		//	NodeName grandParentName = parentName.ParentName;
		//	Node grandParent;

		//	if (grandParentName == NodeName.Root && rootGroup != null)
		//	{
		//		grandParentName = NodeName.From(rootGroup);
		//		grandParent = GetParentNode(grandParentName, null);
		//	}
		//	else
		//	{
		//		grandParent = GetParentNode(grandParentName, rootGroup);
		//	}

		//	parent = new Node(parentName);
		//	parent.NodeType = NodeType.NameSpace;
		//	parent.RootGroup = rootGroup;

		//	AddNode(parent, grandParent);
		//	return parent;
		//}



		//private Node GetParentNode2(NodeName nodeName, ModelNode modelNode)
		//{
		//	NodeName parentName = GetParentName(nodeName, modelNode);

		//	string rootGroup = modelNode.RootGroup;

		//	if (parentName == NodeName.Root && rootGroup != null)
		//	{
		//		parentName = NodeName.From(rootGroup);
		//		return GetParentNode(parentName, null);
		//	}

		//	if (model.TryGetNode(parentName, out Node parent))
		//	{
		//		return parent;
		//	}

		//	// The parent not yet added. We need the grandparent to add parent
		//	NodeName grandParentName = parentName.ParentName;
		//	Node grandParent;

		//	if (grandParentName == NodeName.Root && rootGroup != null)
		//	{
		//		grandParentName = NodeName.From(rootGroup);
		//		grandParent = GetParentNode(grandParentName, null);
		//	}
		//	else
		//	{
		//		grandParent = GetParentNode(grandParentName, rootGroup);
		//	}

		//	parent = new Node(parentName);
		//	parent.NodeType = NodeType.NameSpace;
		//	parent.RootGroup = rootGroup;

		//	AddNode(parent, grandParent);
		//	return parent;
		//}


		//private static NodeName GetParentName(NodeName nodeName, ModelNode modelNode)
		//{
		//	return nodeName.ParentName;
		//}


		//private void MoveNodeIfNeeded(Node node, string rootGroup)
		//{
		//	if (rootGroup == null || node.Name == NodeName.Root || node.RootGroup != null)
		//	{
		//		return;
		//	}

		//	if (node.Parent.Name != NodeName.Root && node.Parent.RootGroup == null)
		//	{
		//		// Parent needs to be moved
		//		MoveNodeIfNeeded(node.Parent, rootGroup);
		//		node.RootGroup = rootGroup;
		//	}
		//	else if (node.Parent.Name == NodeName.Root)
		//	{
		//		// This node needs to be moved
		//		NodeName parentName = NodeName.From(rootGroup);
		//		Node parent = GetParentNode(parentName, null);
		//		MoveNode(node, parent);

		//		node.RootGroup = rootGroup;
		//	}
		//}


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
			node.ItemsCanvas = new ItemsCanvas(node.ViewModel);
			parentCanvas.AddChildCanvas(node.ItemsCanvas);
			node.ViewModel.ItemsViewModel = new ItemsViewModel(node.ItemsCanvas);

			if (Math.Abs(node.ScaleFactor) > 0.0000001)
			{
				node.ItemsCanvas.Scale = node.ScaleFactor * node.Parent.ItemsCanvas.Scale;
			}

			if (node.Offset != PointEx.Zero)
			{
				node.ItemsCanvas.Offset = node.Offset;
			}

			return node.ItemsCanvas;
		}
	}
}
