using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Dependinator.ApplicationHandling;
using Dependinator.Modeling;
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

		private readonly Model model;
		private readonly WorkingFolder workingFolder;
		private Dispatcher dispatcher;

		public ModelService(
			IModelingService modelingService,
			INodeService nodeService,
			Model model,
			WorkingFolder workingFolder)
		{
			this.modelingService = modelingService;
			this.nodeService = nodeService;
			this.model = model;
			this.workingFolder = workingFolder;
		}


		public void Init(IItemsCanvas rootCanvas)
		{
			dispatcher = Dispatcher.CurrentDispatcher;
			model.Nodes.SetRootCanvas(rootCanvas);
		}


		public async Task RefreshAsync(bool refreshLayout)
		{

			//StoreViewSettings();
			//t.Log("stored setting");

			//ModelViewDataOld modelViewData = refreshLayout ? null : modelingService.ToViewData(currentModel);
			//t.Log("Got current model data");

			////currentModel.Root.Clear();

			ModelViewDataOld modelViewData = null;
			await RefreshElementTreeAsync(modelViewData);

		}

		public void ZoomRoot(double zoomFactor, Point zoomCenter)
		{

		}

		public void MoveRootItems(Vector viewOffset)
		{
			
		}


		private async Task<ModelOld> RefreshElementTreeAsync(ModelViewDataOld modelViewData)
		{
			ModelOld model = await Task.Run(
				() => modelingService.Analyze(workingFolder.FilePath, modelViewData));

			return model;
		}


		public void UpdateNodes(IReadOnlyList<Node> nodes)
		{
			foreach (List<Node> batch in nodes.Partition(100))
			{
				dispatcher.BeginInvoke(
					DispatcherPriority.Background,
					(Action<List<Node>>)(batchNodes =>
					{
						batchNodes.ForEach(UpdateNode);
					}),
					batch);
			}
		}

		public void UpdateLinks(IReadOnlyList<Link> links)
		{
			foreach (List<Link> batch in links.Partition(100))
			{
				dispatcher.BeginInvoke(
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

			NodeViewModel nodeViewModel = new NodeViewModel(nodeService, node);
			model.Nodes.AddViewModel(node.Id, nodeViewModel);

			IItemsCanvas parentCanvas = GetCanvas(node.ParentId);
			parentCanvas.AddItem(nodeViewModel);
		}


		private void AddAncestorsIfNeeded(Node node)
		{
			Node current = node;
			while (!model.Nodes.TryGetNode(current.ParentId, out Node parent))
			{
				// Parent node not yet in model
				parent = new NamespaceNode(current.Name.ParentName);
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
				viewModel = new NodeViewModel(nodeService, node);
				model.Nodes.AddViewModel(node.Id, viewModel);

				parentCanvas.AddItem(viewModel);
			}

			IItemsCanvas itemsCanvas = parentCanvas.CreateChild(viewModel);
			model.Nodes.AddItemsCanvas(node.Id, itemsCanvas);
			return itemsCanvas;
		}


		private void UpdateLink(Link link)
		{
			if (model.Links.TryGetLink(link.Id, out Link existingLink))
			{
				// TODO: Check node properties as well and update if changed
				return;
			}

			//if (!model.Nodes.TryGetNode(link.SourceId, out Node source))
			//{
			//	source = new 
			//}
		}
	}
}