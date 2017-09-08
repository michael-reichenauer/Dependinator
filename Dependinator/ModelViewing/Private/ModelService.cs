using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Dependinator.Common.WorkFolders;
using Dependinator.ModelParsing;
using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Open;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Private
{
	[SingleInstance]
	internal class ModelService : IModelService
	{
		private static readonly int BatchSize = 1000;

		private readonly IParserService parserService;
		private readonly INodeService nodeService;
		private readonly ILinkService linkService;
		private readonly Func<OpenModelViewModel> openModelViewModelProvider;

		private readonly Model model;
		private readonly WorkingFolder workingFolder;

		private int currentStamp;
		private bool isShowingOpenModel = false;

		private OpenModelViewModel modelViewModel = null;


		public ModelService(
			IParserService parserService,
			INodeService nodeService,
			ILinkService linkService,
			Func<OpenModelViewModel> openModelViewModelProvider,
			Model model,
			WorkingFolder workingFolder)
		{
			this.parserService = parserService;
			this.nodeService = nodeService;
			this.linkService = linkService;
			this.openModelViewModelProvider = openModelViewModelProvider;

			this.model = model;
			this.workingFolder = workingFolder;
		}


		public Node Root => model.Root;

		public void SetRootCanvas(ItemsCanvas rootCanvas) => Root.ItemsCanvas = rootCanvas;


		public async Task LoadAsync()
		{
			string dataFilePath = GetDataFilePath();
			int stamp = currentStamp++;

			//if (!await parserService.TryDeserialize(
			//	dataFilePath, items => UpdateDataItems(items, stamp)))
			{
				if (File.Exists(workingFolder.FilePath))
				{
					await parserService.AnalyzeAsync(
						workingFolder.FilePath, items => UpdateDataItems(items, stamp));
				}
			}

			if (!Root.Children.Any())
			{
				isShowingOpenModel = true;
				Root.ItemsCanvas.Scale = 1;
				Root.ItemsCanvas.Offset = new Point(0, 0);
				Root.ItemsCanvas.IsZoomAndMoveEnabled = false;

				modelViewModel = openModelViewModelProvider();

				Root.ItemsCanvas.AddItem(modelViewModel);
			}
			else
			{
				isShowingOpenModel = false;
				Root.ItemsCanvas.IsZoomAndMoveEnabled = true;
				if (modelViewModel != null)
				{
					Root.ItemsCanvas.RemoveItem(modelViewModel);
					modelViewModel = null;
				}
			}
		}


		public void ClearAll() => nodeService.RemoveAll();


		public async Task SaveAsync()
		{
			if (isShowingOpenModel)
			{
				// Nothing to save
				return;
			}

			Timing t = Timing.Start();
			IReadOnlyList<Node> nodes = Root.Descendents().ToList();
			t.Log($"Saving {nodes} nodes");

			IReadOnlyList<ModelItem> items = Convert.ToDataItems(nodes);
			t.Log($"Saving {items} items");

			string dataFilePath = GetDataFilePath();
			await parserService.SerializeAsync(items, dataFilePath);
			t.Log($"Saved {items} items");
		}


		public void Save()
		{
			//if (Root.Children.Count == 1 && Root.Children.First() is OpenModelView)
			Timing t = Timing.Start();
			IReadOnlyList<Node> nodes = Root.Descendents().ToList();
			t.Log($"Saving {nodes.Count} nodes");

			IReadOnlyList<ModelItem> items = Convert.ToDataItems(nodes);
			t.Log($"Saving {items.Count} items");

			string dataFilePath = GetDataFilePath();

			parserService.Serialize(items, dataFilePath);
			t.Log($"Saved {items.Count} items");
		}


		public async Task RefreshAsync(bool refreshLayout)
		{
			int stamp = currentStamp++;
			await parserService.AnalyzeAsync(
				workingFolder.FilePath, items => UpdateDataItems(items, stamp));

			nodeService.RemoveObsoleteNodesAndLinks(stamp);

			if (refreshLayout)
			{
				nodeService.ResetLayout();
			}
		}



		public void UpdateDataItems(IReadOnlyList<ModelItem> items, int stamp) =>
			items.Partition(BatchSize)
			.ForEach(batch => Application.Current.Dispatcher.InvokeBackground(UpdateItems, batch, stamp));


		private void UpdateItems(IReadOnlyList<ModelItem> items, int stamp)
		{
			foreach (ModelItem item in items)
			{
				if (item.Node != null)
				{
					nodeService.UpdateNode(item.Node, stamp);
				}

				if (item.Link != null)
				{
					linkService.UpdateLink(item.Link, stamp);
				}
			}
		}

		private string GetDataFilePath() => Path.Combine(workingFolder, "data.json");
	}
}