using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Dependinator.Common.ModelMetadataFolders;
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
		private readonly IRecentModelsService recentModelsService;
		private readonly Func<OpenModelViewModel> openModelViewModelProvider;

		private readonly Model model;
		private readonly ModelMetadata modelMetadata;

		private int currentStamp;
		private bool isShowingOpenModel = false;


		public ModelService(
			IParserService parserService,
			INodeService nodeService,
			ILinkService linkService,
			Func<OpenModelViewModel> openModelViewModelProvider,
			Model model,
			ModelMetadata modelMetadata,
			IRecentModelsService recentModelsService)
		{
			this.parserService = parserService;
			this.nodeService = nodeService;
			this.linkService = linkService;
			this.openModelViewModelProvider = openModelViewModelProvider;

			this.model = model;
			this.modelMetadata = modelMetadata;
			this.recentModelsService = recentModelsService;
		}


		public Node Root => model.Root;

		public void SetRootCanvas(ItemsCanvas rootCanvas) => Root.ItemsCanvas = rootCanvas;


		public async Task LoadAsync()
		{
			string dataFilePath = GetDataFilePath();
			int stamp = currentStamp++;

			ClearAll();

			//if (File.Exists(dataFilePath))
			//{
			//	await parserService.TryDeserialize(
			//		dataFilePath, items => UpdateDataItems(items, stamp));
			//}
			//else
			if (File.Exists(modelMetadata.ModelFilePath))
			{
				await parserService.AnalyzeAsync(
					modelMetadata.ModelFilePath, items => UpdateDataItems(items, stamp));
			}

			if (!Root.Children.Any())
			{
				isShowingOpenModel = true;
				Root.ItemsCanvas.Scale = 1;
				Root.ItemsCanvas.Offset = new Point(0, 0);
				Root.ItemsCanvas.IsZoomAndMoveEnabled = false;

				Root.ItemsCanvas.AddItem(openModelViewModelProvider());
			}
			else
			{
				isShowingOpenModel = false;
				Root.ItemsCanvas.IsZoomAndMoveEnabled = true;
				recentModelsService.AddModelPaths(modelMetadata.ModelFilePath);
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
				modelMetadata.ModelFilePath, items => UpdateDataItems(items, stamp));

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

		private string GetDataFilePath() => Path.Combine(modelMetadata, "data.json");
	}
}