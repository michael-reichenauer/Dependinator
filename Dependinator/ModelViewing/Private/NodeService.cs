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
	internal class NodeService : INodeService
	{
		private readonly INodeViewModelService nodeViewModelService;
		private readonly ILinkService linkService;
		private readonly Model model;


		public NodeService(
			Model model,
			ILinkService linkService,
			INodeViewModelService nodeViewModelService)
		{
			this.model = model;
			this.linkService = linkService;
			this.nodeViewModelService = nodeViewModelService;
		}

		public void UpdateNode(ModelNode modelNode, int stamp)
		{
			NodeName name = modelNode.Name;
			
			if (model.TryGetNode(name, out Node existingNode))
			{
				// TODO: Check node properties as well and update if changed
				existingNode.Stamp = stamp;

				if (existingNode.NodeType != NodeType.Referenced)
				{
					if (existingNode.NodeType.AsString() != modelNode.NodeType)
					{
						existingNode.NodeType = new NodeType(modelNode.NodeType);
						UpdateNodeType(existingNode);
					}

					return;
				}
			}

			NodeName parentName = name.ParentName;
			Node parentNode = GetNode(parentName, modelNode.RootGroup);

			Node node = existingNode ?? new Node(name)
			{
				Stamp = stamp,
				NodeType = new NodeType(modelNode.NodeType),
				Bounds = modelNode.Bounds,
				Scale = modelNode.ItemsScale,
				Offset = modelNode.ItemsOffset,
				Color = modelNode.Color
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
					linkService.RemoveObsoleteLinks(obsoleteLinks);

					RemoveNode(node);
				}
				else
				{
					List<Link> obsoleteLinks = node.SourceLinks.ToList();
					linkService.RemoveObsoleteLinks(obsoleteLinks);
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
					linkService.RemoveObsoleteLinks(obsoleteLinks);

					RemoveNode(node);
				}
				else
				{
					List<Link> obsoleteLinks = node.SourceLinks.Where(link => link.Stamp != stamp).ToList();
					linkService.RemoveObsoleteLinks(obsoleteLinks);
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
				linkService.ResetLayout(links);
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

			RemoveNodeFromParentCanvas(node);
		}

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


		private void RemoveNodeFromParentCanvas(Node node)
		{
			node.Parent?.ItemsCanvas?.RemoveItem(node.ViewModel);
		}


		private Node GetNode(NodeName nodeName, string rootGroup)
		{
			if (model.TryGetNode(nodeName, out Node node))
			{
				return node;
			}

			// The node not yet added. We need the parent to add the node
			NodeName parentName = nodeName.ParentName;

			if (rootGroup != null && parentName == NodeName.Root)
			{
				parentName = new NodeName(rootGroup).ParentName;

				Node parent = GetNode(parentName, null);
				node = new Node(nodeName);
				node.NodeType = NodeType.NameSpace;
				AddNode(node, parent);
				return node;
			}
			else
			{
				Node parent = GetNode(parentName, rootGroup);
				node = new Node(nodeName);
				node.NodeType = NodeType.NameSpace;
				AddNode(node, parent);
				return node;
			}
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

			if (node.Scale != 0)
			{
				node.ItemsCanvas.Scale = node.Scale;
			}

			if (node.Offset != PointEx.Zero)
			{
				node.ItemsCanvas.Offset = node.Offset;
			}

			return node.ItemsCanvas;
		}
	}
}