using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.Common;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.ModelHandling.Private
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
			model.Root.Descendents().ForEach(node => node.View.IsLayoutCompleted = true);
		}


		public void RemoveAll()
		{
			model.Root?.View.ItemsCanvas?.RemoveAll();

			model.RemoveAll();
		}


		public IReadOnlyList<NodeName> GetHiddenNodeNames()
			=> model.Root.Descendents()
				.Where(node => node.View.IsHidden)
				.Select(node => node.Name)
				.ToList();


		public void ShowHiddenNode(NodeName nodeName)
		{
			if (model.TryGetNode(nodeName, out Node node))
			{
				node?.View.ShowHiddenNode();
			}
		}


		private void UpdateNode(Node node, ModelNode modelNode, int stamp)
		{
			node.Stamp = stamp;

			// MoveNodeIfNeeded(node, modelNode);

			UpdateDescriptionIfNeeded(node, modelNode);

			UpdateNodeTypeIfNeeded(node, modelNode);
		}


		private void AddNodeToModel(NodeName name, ModelNode modelNode, int stamp)
		{
			Node node = new Node(name)
			{
				Stamp = stamp,
				NodeType = modelNode.NodeType,
				Description = modelNode.Description,
			
			};

			node.View.Bounds = modelNode.Bounds;
			node.View.ScaleFactor = modelNode.ItemsScaleFactor;
			node.View.Color = modelNode.Color;
			node.View.IsHidden = modelNode.ShowState == Node.Hidden;

			Node parentNode = GetParentNode(name, modelNode);

			AddNode(node, parentNode);
		}


		private void AddNode(Node node, Node parentNode)
		{
			model.Add(node);
			parentNode.AddChild(node);

			CreateNodeViewModel(node);

			AddNodeToParentCanvas(node, parentNode);

			if (model.TryGetQueuedLinesAndLinks(
				node.Name, 
				out IReadOnlyList<ModelLine> lines,
				out IReadOnlyList<ModelLink> links))
			{
				lines.ForEach(line => modelLinkService.UpdateLine(line, node.Stamp));
				links.ForEach(link => modelLinkService.UpdateLink(link, node.Stamp));
				model.RemovedQueuedNode(node.Name);
			}
		}


		private void RemoveNode(Node node)
		{
			model.Remove(node);
			node.Parent?.RemoveChild(node);

			if (node.View.ItemsCanvas != null)
			{
				node.Parent?.View.ItemsCanvas.RemoveChildCanvas(node.View.ItemsCanvas);

			}

			RemoveNodeFromParentCanvas(node);
		}



		private void UpdateNodeTypeIfNeeded(Node node, ModelNode modelNode)
		{
			if (node.NodeType != modelNode.NodeType)
			{
				Log.Warn($"Node type has changed for {node} to {modelNode.NodeType}");

				node.NodeType = modelNode.NodeType;

				RemoveNodeFromParentCanvas(node);
				CreateNodeViewModel(node);
				AddNodeToParentCanvas(node, node.Parent);
			}
		}

		private static void UpdateDescriptionIfNeeded(Node node, ModelNode modelNode)
		{
			if (!string.IsNullOrEmpty(modelNode.Description)
				&& node.Description != modelNode.Description)
			{
				node.Description = modelNode.Description;
			}
		}

		private void CreateNodeViewModel(Node node)
		{
			if (node.NodeType == NodeType.Member)
			{
				node.View.ViewModel = new MemberNodeViewModel(nodeViewModelService, node);
			}
			else if (node.NodeType == NodeType.Type)
			{
				node.View.ViewModel = new TypeViewModel(nodeViewModelService, node);
				node.View.ItemsCanvas = GetItemsCanvas(node);
			}
			else
			{
				node.View.ViewModel = new NamespaceViewModel(nodeViewModelService, node);
				node.View.ItemsCanvas = GetItemsCanvas(node);
			}
		}


		private void AddNodeToParentCanvas(Node node, Node parentNode)
		{
			try
			{
				layoutService.SetLayout(node.View.ViewModel);

				ItemsCanvas parentCanvas = parentNode.View.ItemsCanvas;

				parentCanvas.AddItem(node.View.ViewModel);
			}
			catch (Exception e)
			{
				Log.Exception(e, $"Failed adding {node} to parent {parentNode}");
				throw;
			}
		}


		private static void RemoveNodeFromParentCanvas(Node node)
		{
			node.Parent?.View.ItemsCanvas?.RemoveItem(node.View.ViewModel);
		}


		private Node GetParentNode(NodeName nodeName, ModelNode modelNode)
		{
			NodeName parentName = GetParentName(nodeName, modelNode);

			if (model.TryGetNode(parentName, out var parent))
			{
				return parent;
			}

			NodeType nodeType = modelNode.NodeType == NodeType.Member ? NodeType.Type : NodeType.NameSpace;
			Node parentNode = GetParentNode(parentName, nodeType);
			return parentNode;
		}


		private Node GetParentNode(NodeName parentName, NodeType nodeType)
		{
			if (model.TryGetNode(parentName, out Node parent))
			{
				return parent;
			}

			NodeName grandParentName = parentName.ParentName;

			Node grandParent = GetParentNode(grandParentName, NodeType.NameSpace);

			parent = new Node(parentName);
			parent.NodeType = nodeType;

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
			if (node.View.ItemsCanvas != null)
			{
				return node.View.ItemsCanvas;
			}

			// The node does not yet have a canvas. So we need to get the parent canvas and
			// then create a child canvas for this node.
			ItemsCanvas parentCanvas = GetItemsCanvas(node.Parent);

			// Creating the child canvas to be the children canvas of the node
			node.View.ItemsCanvas = new ItemsCanvas(node.View.ViewModel, parentCanvas);
			node.View.ViewModel.ItemsViewModel = new ItemsViewModel(node.View.ItemsCanvas);

			if (Math.Abs(node.View.ScaleFactor) > 0.0000001)
			{
				node.View.ItemsCanvas.SetScaleFactor(node.View.ScaleFactor);
			}

			//if (node.Offset != PointEx.Zero)
			//{
			//	node.ItemsCanvas.SetMoveOffset(node.Offset);
			//}

			return node.View.ItemsCanvas;
		}
	}
}
