using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using Dependinator.ApplicationHandling;
using Dependinator.Modeling;
using Dependinator.Modeling.Private.Serializing;
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
			model.Nodes.SetRootCanvas(rootCanvas);
		}


		public async Task LoadAsync()
		{
			await Task.Run(() => modelingService.Analyze(workingFolder.FilePath));
		}


		public async Task RefreshAsync(bool refreshLayout)
		{

			await Task.Run(() => modelingService.Analyze(workingFolder.FilePath));
		}



		public void UpdateNodes(IReadOnlyList<Node> nodes)
		{
			foreach (List<Node> batch in nodes.Partition(100))
			{
				dispatcher.Invoke(
					DispatcherPriority.Background,
					(Action<List<Node>>)(batchNodes => { batchNodes.ForEach(UpdateNode); }),
					batch);
			}
		}

		public void UpdateLinks(IReadOnlyList<Link> links)
		{
			foreach (List<Link> batch in links.Partition(100))
			{
				dispatcher.Invoke(
					DispatcherPriority.Background,
					(Action<List<Link>>)(batchLinks => { batchLinks.ForEach(UpdateLink); }),
					batch);
			}
		}


		private void UpdateNode(Node node)
		{
			if (model.Nodes.TryGetNode(node.Id, out Node existingNode))
			{
				// TODO: Check node properties as well and update if changed
				return;
			}

			AddAncestorsIfNeeded(node);

			IItemsCanvas parentCanvas = GetCanvas(node.ParentId);

			model.Nodes.Add(node);
			AddNode(node, parentCanvas);
		}


		private void AddAncestorsIfNeeded(Node node)
		{
			Node current = node;
			while (!model.Nodes.TryGetNode(current.ParentId, out Node parent))
			{
				// Parent node not yet in model, assume Namespace
				parent = new Node(current.Name.ParentName, Data.NodeType.NameSpaceType);
				model.Nodes.Add(parent);
				current = parent;
			}
		}


		private IItemsCanvas GetCanvas(NodeId nodeId)
		{
			// First trying to get ann previously created items canvas
			if (model.Nodes.TryGetItemsCanvas(nodeId, out IItemsCanvas itemsCanvas))
			{
				return itemsCanvas;
			}

			// The node does not yet have a canvas. So we need to get the parent canvas and
			// then create a child canvas for this node. But since the parent might also not yet
			// have a canvas, we traverse the ancestors of the node and create all the needed
			// canvases. Lets start by getting the node and ancestors, until we find one with
			// a canvas (at least root node will have one)
			var ancestors = model.Nodes
				.GetAncestorsAndSelf(nodeId)
				.TakeWhile(ancestor => !model.Nodes.TryGetItemsCanvas(ancestor.Id, out itemsCanvas));

			// Creating items canvases from the top down to the node and adding each as a child
			foreach (Node ancestor in ancestors.Reverse())
			{
				itemsCanvas = AddCompositeNode(ancestor, itemsCanvas);
			}

			return itemsCanvas;
		}


		private IItemsCanvas AddCompositeNode(Node node, IItemsCanvas parentCanvas)
		{
			if (!model.Nodes.TryGetViewModel(node.Id, out NodeViewModel viewModel))
			{
				viewModel = AddNode(node, parentCanvas);
			}

			IItemsCanvas itemsCanvas = parentCanvas.CreateChild(viewModel);
			itemsCanvas.SetInitialScale(parentCanvas.Scale / 7);

			if (viewModel is CompositeNodeViewModel composite)
			{
				composite.ItemsViewModel = new ItemsViewModel(itemsCanvas);
			}
			
			model.Nodes.AddItemsCanvas(node.Id, itemsCanvas);
			return itemsCanvas;
		}


		private NodeViewModel AddNode(Node node, IItemsCanvas parentCanvas)
		{
			NodeViewModel nodeViewModel = CreateNodeViewModel(node);

			nodeService.SetLayout(nodeViewModel);
			model.Nodes.AddViewModel(node.Id, nodeViewModel);
			parentCanvas.AddItem(nodeViewModel);

			return nodeViewModel;
		}


		private NodeViewModel CreateNodeViewModel(Node node)
		{
			NodeViewModel nodeViewModel;
			if (node.NodeType == Data.NodeType.TypeType)
			{
				nodeViewModel = new TypeViewModel(nodeService, node);
			}
			else if (node.NodeType == Data.NodeType.MemberType)
			{
				nodeViewModel = new MemberNodeViewModel(nodeService, node);
			}
			else
			{
				nodeViewModel = new NamespaceViewModel(nodeService, node);
			}

			return nodeViewModel;
		}
	

		private void UpdateLink(Link link)
		{
			if (model.Links.TryGetLink(link.Id, out Link existingLink))
			{
				// TODO: Check node properties as well and update if changed
				return;
			}


			model.Links.Add(link);

			model.Nodes.TryGetNode(link.SourceId, out Node sourceNode);
			model.Nodes.TryGetNode(link.TargetId, out Node targetNode);

			if (sourceNode.ParentId != targetNode.ParentId)
			{
				return;
			}

			if (sourceNode == targetNode)
			{
				return;
			}

			IItemsCanvas parentCanvas = GetCanvas(sourceNode.ParentId);

			model.Nodes.TryGetViewModel(link.SourceId, out NodeViewModel source);
			model.Nodes.TryGetViewModel(link.TargetId, out NodeViewModel target);

			LineViewModel lineViewModel = new LineViewModel(linkService, source, target);
			lineViewModel.ItemZIndex = -1;
			parentCanvas.AddItem(lineViewModel);
		}	
	}
}