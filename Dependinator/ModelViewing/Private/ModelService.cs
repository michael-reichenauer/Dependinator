using System;
using System.Collections.Generic;
using System.IO;
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
		private static readonly int BatchSize = 1000;

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


		public Node Root => model.Root;


		public void Init(ItemsCanvas rootCanvas)
		{
			dispatcher = Dispatcher.CurrentDispatcher;
			Root.ItemsCanvas = rootCanvas;
		}


		public async Task LoadAsync()
		{
			string dataFilePath = GetDataFilePath();
			if (!await modelingService.TryDeserialize(dataFilePath))
			{
				await modelingService.AnalyzeAsync(workingFolder.FilePath);
			}		
		}


		public async Task SaveAsync()
		{
			Timing t = Timing.Start();
			IReadOnlyList<Node> nodes = model.Root.Descendents().ToList();
			t.Log($"Saving {nodes} nodes");

			IReadOnlyList<DataItem> items = Convert.ToDataItems(nodes);
			t.Log($"Saving {items} items");

			string dataFilePath = GetDataFilePath();
			await modelingService.SerializeAsync(items, dataFilePath);
			t.Log($"Saved {items} items");
		}


		public void Save()
		{
			Timing t = Timing.Start();
			IReadOnlyList<Node> nodes = model.Root.Descendents().ToList();
			t.Log($"Saving {nodes.Count} nodes");

			IReadOnlyList<DataItem> items = Convert.ToDataItems(nodes);
			t.Log($"Saving {items.Count} items");

			string dataFilePath = GetDataFilePath();

			modelingService.Serialize(items, dataFilePath);
			t.Log($"Saved {items.Count} items");
		}


		public async Task RefreshAsync(bool refreshLayout)
		{
			await modelingService.AnalyzeAsync(workingFolder.FilePath);
		}


		public void UpdateDataItems(IReadOnlyList<DataItem> items) =>
			items.Partition(BatchSize).ForEach(batch => dispatcher.Invoke(UpdateItems, batch));


		private void UpdateItems(List<DataItem> items)
		{
			foreach (DataItem item in items)
			{
				if (item.Node != null)
				{
					nodeService.UpdateNode(item.Node);
				}

				if (item.Link != null)
				{
					linkService.UpdateLink(item.Link);
				}
			}
		}

		private string GetDataFilePath() => Path.Combine(workingFolder, "data.json");
	}
}