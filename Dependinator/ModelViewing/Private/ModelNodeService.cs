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
				UpdateNode(existingNode, modelNode, stamp);
				return;
			}

			AddNode(modelNode, stamp, name);
		}


		private void UpdateNode(Node node, ModelNode modelNode, int stamp)
		{
			node.Stamp = stamp;

			MoveNodeIfNeeded(node, modelNode);

			UpdateNodeTypeIfNeeded(node, modelNode);
		}


		private void AddNode(ModelNode modelNode, int stamp, NodeName name)
		{
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
			};

			AddNode(node, parentNode);
		}


		private void AddNode(Node node, Node parentNode)
		{
			model.Add(node);
			parentNode.AddChild(node);

			CreateNodeViewModel(node);

			AddNodeToParentCanvas(node, parentNode);
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


		private void MoveNodeIfNeeded(Node node, ModelNode modelNode)
		{
			if (modelNode.Parent == null || node.Parent.Name.IsSame(modelNode.Parent))
			{
				// No need to move node
				return;
			}

			Log.Warn($"Node '{node}' needs to be moved from '{node.Parent}' to {modelNode.Parent}");


			//	node.Parent?.RemoveChild(node);

			//	if (node.ItemsCanvas != null)
			//	{
			//		node.Parent?.ItemsCanvas.RemoveChildCanvas(node.ItemsCanvas);
			//	}

			//RemoveNodeFromParentCanvas(node);

			//	parentNode.AddChild(node);
			//	ItemsCanvas parentCanvas = GetChildrenCanvas(parentNode);
			//	if (node.ItemsCanvas != null)
			//	{
			//		parentCanvas.AddChildCanvas(node.ItemsCanvas);
			//	}

			//	parentCanvas.AddItem(node.ViewModel);
		}



		private void UpdateNodeTypeIfNeeded(Node node, ModelNode modelNode)
		{
			if (!node.NodeType.IsSame(modelNode.NodeType))
			{
				node.NodeType = new NodeType(modelNode.NodeType);

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
			try
			{
				nodeViewModelService.SetLayout(node.ViewModel);

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

			Stack<NodeName> ancestorNames = new Stack<NodeName>();
			ancestorNames.Push(parentName);

			while (true)
			{
				NodeName name = ancestorNames.Peek();
				parentName = name.ParentName;

				if (parentName == NodeName.Root && !name.FullName.StartsWithTxt("$"))
				{
					// Group external assemblies if assembly name consists of parts 
					int index = name.FullName.IndexOfTxt("*");
					if (index > 0)
					{
						string groupName = name.FullName.Substring(0, index);
						parentName = NodeName.From($"${groupName}");
					}
				}

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


		private static NodeName GetParentName(NodeName nodeName, ModelNode modelNode)
		{
			NodeName parentName = modelNode.Parent != null ? NodeName.From(modelNode.Parent) : nodeName.ParentName;

			return parentName;
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
