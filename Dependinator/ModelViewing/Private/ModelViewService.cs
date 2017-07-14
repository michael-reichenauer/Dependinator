using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Dependinator.ApplicationHandling;
using Dependinator.ApplicationHandling.SettingsHandling;
using Dependinator.Modeling;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.ModelViewing.Private.Items.Private;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private
{
	[SingleInstance]
	internal class ModelViewService : IModelViewService, IModelNotifications
	{
		private readonly WorkingFolder workingFolder;
		private readonly IModelingService modelingService;
		private readonly INodeService nodeService;
		private readonly Model model;

		private Dispatcher dispatcher;

		private ModelOld currentModel;

		public ModelViewService(
			WorkingFolder workingFolder,
			IModelingService modelingService,
			INodeService nodeService,
			Model model)
		{
			this.workingFolder = workingFolder;
			this.modelingService = modelingService;
			this.nodeService = nodeService;
			this.model = model;
		}





		public void InitModules(IItemsCanvas rootCanvas)
		{
			dispatcher = Dispatcher.CurrentDispatcher;
			model.Nodes.SetRootCanvas(rootCanvas);

			Timing t = new Timing();

			currentModel = GetDataModel();

			t.Log($"Get data model {currentModel}");

			//model = modelingService.ToModel(dataModel, null);

			t.Log("To model");

			ShowModel(rootCanvas);
			t.Log("Show model");

			t.Log("Showed model");
		}


		private ModelOld GetDataModel()
		{
			ModelOld dataModel = GetCachedOrFreshModelData();

			return dataModel;
		}


		public async Task Refresh(IItemsCanvas rootCanvas, bool refreshLayout)
		{
			await Task.Yield();

			Timing t = new Timing();

			StoreViewSettings();
			t.Log("stored setting");

			ModelViewDataOld modelViewData = refreshLayout ? null : modelingService.ToViewData(currentModel);
			t.Log("Got current model data");

			//currentModel.Root.Clear();

			await RefreshElementTreeAsync(modelViewData);


			t.Log("Read fresh data");

			//ShowModel(rootCanvas);

			t.Log("Show model");

			t.Log("Refreshed model");
		}


		private ModelOld GetCachedOrFreshModelData()
		{
			ModelOld dataModel;
			if (!TryReadCachedData(out dataModel))
			{
				dataModel = ReadFreshData();
			}

			return dataModel;
		}


		private void ShowModel(IItemsCanvas rootCanvas)
		{
			RestoreViewSettings(rootCanvas);

			NodeOld rootNode = currentModel.Root;

			rootNode.Show(rootCanvas);
		}


		public void Zoom(double zoomFactor, Point zoomCenter) =>
			currentModel.Root.Zoom(zoomFactor, zoomCenter);


		public void Move(Vector viewOffset)
		{
			currentModel.Root.MoveItems(viewOffset);
		}


		private async Task<ModelOld> RefreshElementTreeAsync(ModelViewDataOld modelViewData)
		{
			ModelOld model = await Task.Run(
				() => modelingService.Analyze(workingFolder.FilePath, modelViewData));

			return model;
		}


		private bool TryReadCachedData(out ModelOld dataModel)
		{
			string dataFilePath = GetDataFilePath();
			return modelingService.TryDeserialize(dataFilePath, out dataModel);
		}


		private ModelOld ReadFreshData()
		{
			Timing t = Timing.Start();
			ModelOld newModel = modelingService.Analyze(workingFolder.FilePath, null);
			t.Log("Read fresh model");
			return newModel;
		}


		public void Close()
		{
			currentModel.Root.UpdateAllNodesScalesBeforeClose();
			//DataModel dataModel = modelingService.ToDataModel(model);
			string dataFilePath = GetDataFilePath();

			modelingService.Serialize(currentModel, dataFilePath);

			StoreViewSettings();
		}


		private string GetDataFilePath()
		{
			return Path.Combine(workingFolder, "data.json");
		}


		private void StoreViewSettings()
		{
			Settings.EditWorkingFolderSettings(workingFolder,
				settings =>
				{
					settings.Scale = currentModel.Root.ItemsScale;
					settings.X = currentModel.Root.ItemsOffset.X;
					settings.Y = currentModel.Root.ItemsOffset.Y;
				});
		}


		private void RestoreViewSettings(IItemsCanvas rootCanvas)
		{
			WorkFolderSettings settings = Settings.GetWorkFolderSetting(workingFolder);
			rootCanvas.Scale = settings.Scale;
			rootCanvas.Offset = new Point(settings.X, settings.Y);
		}


		public void UpdateNodes(IReadOnlyList<Node> nodes)
		{
			foreach (List<Node> batch in nodes.Partition(100))
			{
				dispatcher.BeginInvoke(
					DispatcherPriority.Background,
					(Action<List<Node>>)(batchNodes => { batchNodes.ForEach(UpdateNode); }),
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