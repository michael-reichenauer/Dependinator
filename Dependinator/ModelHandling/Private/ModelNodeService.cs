using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.Common;
using Dependinator.ModelHandling.Core;
using Dependinator.ModelHandling.Private.Items;
using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;


namespace Dependinator.ModelHandling.Private
{
	internal class ModelNodeService : IModelNodeService
	{
		private readonly INodeViewModelService nodeViewModelService;
		private readonly INodeLayoutService layoutService;
		private readonly IModelLinkService modelLinkService;
		private readonly Model model;


		public ModelNodeService(
			Model model,
			IModelLinkService modelLinkService,
			INodeViewModelService nodeViewModelService,
			INodeLayoutService layoutService)
		{
			this.model = model;
			this.modelLinkService = modelLinkService;
			this.nodeViewModelService = nodeViewModelService;
			this.layoutService = layoutService;
		}


		public void UpdateNode(ModelNode modelNode, int stamp)
		{
			NodeName name = NodeName.From(modelNode.Name);

			if (model.TryGetNode(name, out Node node))
			{
				UpdateNode(node, modelNode, stamp);
				return;
			}

			AddNodeToModel(name, modelNode, stamp);
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
				if (node.Stamp != stamp && node.NodeType == NodeType.NameSpace && !node.Children.Any())
				{
					// Node is an empty namespace, lets remove it
					RemoveNode(node);
				}
			}
			
			t.Log($"{nodes.Count} nodes");
		}


		public void SetLayoutDone()
		{
			model.Root.Descendents().ForEach(node => node.IsLayoutCompleted = true);
		}


		public void RemoveAll()
		{
			model.Root?.ItemsCanvas?.RemoveAll();

			model.RemoveAll();
		}


		public IReadOnlyList<NodeName> GetHiddenNodeNames()
			=> model.Root.Descendents()
				.Where(node => node.IsHidden)
				.Select(node => node.Name)
				.ToList();


		public void ShowHiddenNode(NodeName nodeName)
		{
			if (model.TryGetNode(nodeName, out Node node))
			{
				node?.ShowHiddenNode();
			}
		}


		private void UpdateNode(Node node, ModelNode modelNode, int stamp)
		{
			node.Stamp = stamp;

			// MoveNodeIfNeeded(node, modelNode);

			UpdateNodeTypeIfNeeded(node, modelNode);
		}


		private void AddNodeToModel(NodeName name, ModelNode modelNode, int stamp)
		{
			Node node = new Node(name)
			{
				Stamp = stamp,
				NodeType = modelNode.NodeType,
				Bounds = modelNode.Bounds,
				ScaleFactor = modelNode.ItemsScaleFactor,
				Offset = modelNode.ItemsOffset,
				Color = modelNode.Color,
				IsHidden = modelNode.ShowState == Node.Hidden
			};

			Node parentNode = GetParentNode(name, modelNode);

			AddNode(node, parentNode);
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


		//private void MoveNodeIfNeeded(Node node, ModelNode modelNode)
		//{
		//	//if (modelNode.Parent == null || node.Parent.Name.IsSame(modelNode.Parent))
		//	//{
		//	//	// No need to move node
		//	//	return;
		//	//}

		//	//Log.Warn($"Node '{node}' needs to be moved from '{node.Parent}' to {modelNode.Parent}");


		//	//	node.Parent?.RemoveChild(node);

		//	//	if (node.ItemsCanvas != null)
		//	//	{
		//	//		node.Parent?.ItemsCanvas.RemoveChildCanvas(node.ItemsCanvas);
		//	//	}

		//	//RemoveNodeFromParentCanvas(node);

		//	//	parentNode.AddChild(node);
		//	//	ItemsCanvas parentCanvas = GetChildrenCanvas(parentNode);
		//	//	if (node.ItemsCanvas != null)
		//	//	{
		//	//		parentCanvas.AddChildCanvas(node.ItemsCanvas);
		//	//	}

		//	//	parentCanvas.AddItem(node.ViewModel);
		//}



		private void UpdateNodeTypeIfNeeded(Node node, ModelNode modelNode)
		{
			if (node.NodeType != modelNode.NodeType)
			{
				Log.Warn($"Node type has changed from {node} to {modelNode.NodeType}");

				node.NodeType = modelNode.NodeType;

				RemoveNodeFromParentCanvas(node);
				CreateNodeViewModel(node);
				AddNodeToParentCanvas(node, node.Parent);
			}
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
				node.ItemsCanvas = GetItemsCanvas(node);
			}
			else
			{
				node.ViewModel = new NamespaceViewModel(nodeViewModelService, node);
				node.ItemsCanvas = GetItemsCanvas(node);
			}
		}


		private void AddNodeToParentCanvas(Node node, Node parentNode)
		{
			try
			{
				layoutService.SetLayout(node.ViewModel);

				ItemsCanvas parentCanvas = parentNode.ItemsCanvas;

				parentCanvas.AddItem(node.ViewModel);
			}
			catch (Exception e)
			{
				Log.Exception(e, $"Failed adding {node} to parent {parentNode}");
				throw;
			}
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

			Node parentNode = GetParentNode(parentName);
			return parentNode;
		}


		private Node GetParentNode(NodeName parentName)
		{
			if (model.TryGetNode(parentName, out Node parent))
			{
				return parent;
			}

			NodeName grandParentName = parentName.ParentName;

			Node grandParent = GetParentNode(grandParentName);

			parent = new Node(parentName);
			parent.NodeType = NodeType.NameSpace;

			AddNode(parent, grandParent);
			return parent;
		}


		private static NodeName GetParentName(NodeName nodeName, ModelNode modelNode)
		{
			NodeName parentName = modelNode.Parent != null
				? NodeName.From(modelNode.Parent) : nodeName.ParentName;

			return parentName;
		}


		private static ItemsCanvas GetItemsCanvas(Node node)
		{
			// First try get existing items canvas
			if (node.ItemsCanvas != null)
			{
				return node.ItemsCanvas;
			}

			// The node does not yet have a canvas. So we need to get the parent canvas and
			// then create a child canvas for this node.
			ItemsCanvas parentCanvas = GetItemsCanvas(node.Parent);

			// Creating the child canvas to be the children canvas of the node
			node.ItemsCanvas = new ItemsCanvas(node.ViewModel, parentCanvas);
			node.ViewModel.ItemsViewModel = new ItemsViewModel(node.ItemsCanvas);

			if (Math.Abs(node.ScaleFactor) > 0.0000001)
			{
				node.ItemsCanvas.SetScaleFactor(node.ScaleFactor);
			}

			if (node.Offset != PointEx.Zero)
			{
				node.ItemsCanvas.SetMoveOffset(node.Offset);
			}

			return node.ItemsCanvas;
		}
	}
}
