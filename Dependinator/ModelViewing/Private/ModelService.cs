using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using Dependinator.ApplicationHandling;
using Dependinator.Modeling;
using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Private
{
	[SingleInstance]
	internal class ModelService : IModelService, IModelNotifications
	{
		private readonly IModelingService modelingService;
		private readonly INodeService nodeService;
		private readonly ILinkService linkService;

		private readonly Model model;
		private readonly WorkingFolder workingFolder;
		private Dispatcher dispatcher;

		public ModelService(
			IModelingService modelingService,
			INodeService nodeService,
			ILinkService linkService,
			Model model,
			WorkingFolder workingFolder)
		{
			this.modelingService = modelingService;
			this.nodeService = nodeService;
			this.linkService = linkService;
			this.model = model;
			this.workingFolder = workingFolder;
		}


		public void Init(IItemsCanvas rootCanvas)
		{
			dispatcher = Dispatcher.CurrentDispatcher;
			model.Nodes.Root.ChildrenCanvas = rootCanvas;
		}


		public async Task LoadAsync()
		{
			await Task.Run(() => modelingService.Analyze(workingFolder.FilePath));
		}


		public async Task RefreshAsync(bool refreshLayout)
		{

			await Task.Run(() => modelingService.Analyze(workingFolder.FilePath));
		}



		public void UpdateNodes(IReadOnlyList<DataNode> nodes)
		{
			foreach (List<DataNode> batch in nodes.Partition(100))
			{
				dispatcher.Invoke(
					DispatcherPriority.Background,
					(Action<List<DataNode>>)(batchNodes => { batchNodes.ForEach(UpdateNode); }),
					batch);
			}
		}

		public void UpdateLinks(IReadOnlyList<DataLink> links)
		{
			foreach (List<DataLink> batch in links.Partition(100))
			{
				dispatcher.Invoke(
					DispatcherPriority.Background,
					(Action<List<DataLink>>)(batchLinks => { batchLinks.ForEach(UpdateLink); }),
					batch);
			}
		}


		private void UpdateNode(DataNode dataNode)
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
			itemsCanvas.SetInitialScale(parentCanvas.Scale / 7);

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

			nodeService.SetLayout(nodeViewModel);
			node.ViewModel = nodeViewModel;
			parentCanvas.AddItem(nodeViewModel);

			return nodeViewModel;
		}


		private NodeViewModel CreateNodeViewModel(Node node)
		{
			NodeViewModel nodeViewModel;
			if (node.NodeType == NodeType.Type)
			{
				nodeViewModel = new TypeViewModel(nodeService, node);
			}
			else if (node.NodeType == NodeType.Member)
			{
				nodeViewModel = new MemberNodeViewModel(nodeService, node);
			}
			else
			{
				nodeViewModel = new NamespaceViewModel(nodeService, node);
			}

			return nodeViewModel;
		}


		private void UpdateLink(DataLink dataLink)
		{
			NodeId sourceId = new NodeId(new NodeName(dataLink.Source));
			NodeId targetId = new NodeId(new NodeName(dataLink.Target));

			Node source = model.Nodes.Node(sourceId);
			Node target = model.Nodes.Node(targetId);

			Link link = new Link(source, target);
			if (source.Links.Contains(link))
			{
				// TODO: Check node properties as well and update if changed
				return;
			}

			if (source == target)
			{
				// Skipping link to self
				return;
			}

			if (source.Parent != target.Parent)
			{
				return;
			}
		

			IItemsCanvas parentCanvas = GetCanvas(source.Parent);

			LineViewModel lineViewModel = new LineViewModel(
				linkService, source.ViewModel, target.ViewModel);

			parentCanvas.AddItem(lineViewModel);
		}
	}
}