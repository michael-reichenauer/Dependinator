using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal class NodeService : INodeService
	{
		private readonly IModelService modelService;
		private readonly IModelLineService modelLineService;
		private readonly IModelLinkService modelLinkService;
		private readonly INodeLayoutService layoutService;
		private readonly Lazy<INodeViewModelService> nodeViewModelService;


		public NodeService(
			IModelService modelService,
			IModelLineService modelLineService,
			IModelLinkService modelLinkService,
			INodeLayoutService layoutService,
			Lazy<INodeViewModelService> nodeViewModelService)
		{
			this.modelService = modelService;
			this.modelLineService = modelLineService;
			this.modelLinkService = modelLinkService;
			this.layoutService = layoutService;
			this.nodeViewModelService = nodeViewModelService;
		}


		public Node Root => modelService.Root;

		public bool TryGetNode(NodeName nodeName, out Node node) => modelService.TryGetNode(nodeName, out node);

		public void QueueNode(DataNode dataNode) => modelService.QueueNode(dataNode);
		public void RemoveAll()
		{
			Root?.View.ItemsCanvas?.RemoveAll();

			modelService.RemoveAll();
		}


		public IEnumerable<Node> AllNodes => modelService.AllNodes;


		public void AddNode(Node node, Node parentNode)
		{
			modelService.Add(node);
			parentNode.AddChild(node);

			CreateNodeViewModel(node);

			AddNodeToParentCanvas(node, parentNode);

			if (modelService.TryGetQueuedLinesAndLinks(
				node.Name,
				out IReadOnlyList<DataLine> lines,
				out IReadOnlyList<DataLink> links))
			{
				lines.ForEach(line => modelLineService.UpdateLine(line, node.Stamp));
				links.ForEach(link => modelLinkService.UpdateLink(link, node.Stamp));
				modelService.RemovedQueuedNode(node.Name);
			}
		}


		public void RemoveNode(Node node)
		{
			modelService.Remove(node);
			node.Parent?.RemoveChild(node);

			if (node.View.ItemsCanvas != null)
			{
				node.Parent?.View.ItemsCanvas.RemoveChildCanvas(node.View.ItemsCanvas);

			}

			RemoveNodeFromParentCanvas(node);
		}


		public void UpdateNodeTypeIfNeeded(Node node, NodeType nodeType)
		{
			if (node.NodeType != nodeType)
			{
				Log.Warn($"Node type has changed for {node} to {nodeType}");

				node.NodeType = nodeType;

				RemoveNodeFromParentCanvas(node);
				CreateNodeViewModel(node);
				AddNodeToParentCanvas(node, node.Parent);
			}
		}


		public Node GetParentNode(NodeName parentName, NodeType childNodeType)
		{
			if (modelService.TryGetNode(parentName, out Node parent))
			{
				return parent;
			}

			NodeType parentNodeType = GetParentNodeType(parentName, childNodeType);

			NodeName grandParentName = parentName.ParentName;
			Node grandParent = GetParentNode(grandParentName, parentNodeType);


			parent = new Node(parentName);
			parent.NodeType = parentNodeType;

			AddNode(parent, grandParent);
			return parent;
		}


		private static NodeType GetParentNodeType(NodeName parentName, NodeType childNodeType)
		{
			if (parentName?.FullName.EndsWith(".$private") ?? false)
			{
				return NodeType.NameSpace;
			}

			return childNodeType == NodeType.Member ? NodeType.Type : NodeType.NameSpace;
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


		private void CreateNodeViewModel(Node node)
		{
			if (node.NodeType == NodeType.Member)
			{
				node.View.ViewModel = new MemberNodeViewModel(nodeViewModelService.Value, node);
			}
			else if (node.NodeType == NodeType.Type)
			{
				node.View.ViewModel = new TypeViewModel(nodeViewModelService.Value, node);
				node.View.ItemsCanvas = GetItemsCanvas(node);
			}
			else
			{
				node.View.ViewModel = new NamespaceViewModel(nodeViewModelService.Value, node);
				node.View.ItemsCanvas = GetItemsCanvas(node);
			}
		}
		private ItemsCanvas GetItemsCanvas(Node node)
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
			node.View.ViewModel.ItemsViewModel = new ItemsViewModel(
				node.View.ItemsCanvas, node.View.ViewModel);

			if (Math.Abs(node.View.ScaleFactor) > 0.0000001)
			{
				node.View.ItemsCanvas.ScaleFactor = node.View.ScaleFactor;
			}

			//if (node.Offset != PointEx.Zero)
			//{
			//	node.ItemsCanvas.SetMoveOffset(node.Offset);
			//}

			return node.View.ItemsCanvas;
		}



		private static void RemoveNodeFromParentCanvas(Node node)
		{
			node.Parent?.View.ItemsCanvas?.RemoveItem(node.View.ViewModel);
		}
	}
}