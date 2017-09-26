using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.ModelParsing;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Open;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private
{
	[SingleInstance]
	internal class ModelService : IModelService
	{
		private static readonly int MaxPriority = 10;
		private static readonly int BatchSize = 1000;

		private readonly IParserService parserService;
		private readonly IModelNodeService modelNodeService;
		private readonly IModelLinkService modelLinkService;
		private readonly IRecentModelsService recentModelsService;
		private readonly Func<OpenModelViewModel> openModelViewModelProvider;

		private readonly Model model;
		private readonly ModelMetadata modelMetadata;

		private int currentId;
		private bool isShowingOpenModel = false;


		public ModelService(
			IParserService parserService,
			IModelNodeService modelNodeService,
			IModelLinkService modelLinkService,
			Func<OpenModelViewModel> openModelViewModelProvider,
			Model model,
			ModelMetadata modelMetadata,
			IRecentModelsService recentModelsService)
		{
			this.parserService = parserService;
			this.modelNodeService = modelNodeService;
			this.modelLinkService = modelLinkService;
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

			ClearAll();

			if (File.Exists(dataFilePath))
			{
				await ShowModelAsync(operation => parserService.TryDeserialize(
					dataFilePath, items => UpdateDataItems(items, operation)));
			}
			else
			if (File.Exists(modelMetadata.ModelFilePath))
			{
				await ShowModelAsync(operation => parserService.AnalyzeAsync(
					modelMetadata.ModelFilePath, items => UpdateDataItems(items, operation)));
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


		public void ClearAll() => modelNodeService.RemoveAll();


		public async Task SaveAsync()
		{
			if (isShowingOpenModel)
			{
				// Nothing to save
				return;
			}

			Timing t = Timing.Start();
			IReadOnlyList<Node> nodes = Root.DescendentsBreadth().ToList();
			t.Log($"Saving {nodes} nodes");

			IReadOnlyList<ModelItem> items = Convert.ToDataItems(nodes);
			t.Log($"Saving {items} items");

			string dataFilePath = GetDataFilePath();
			await parserService.SerializeAsync(items, dataFilePath);
			t.Log($"Saved {items} items");
		}


		public void Save()
		{
			Timing t = Timing.Start();
			IReadOnlyList<Node> nodes = Root.DescendentsBreadth().ToList();
			t.Log($"Saving {nodes.Count} nodes");

			IReadOnlyList<ModelItem> items = Convert.ToDataItems(nodes);
			t.Log($"Saving {items.Count} items");

			string dataFilePath = GetDataFilePath();

			parserService.Serialize(items, dataFilePath);
			t.Log($"Saved {items.Count} items");
		}


		public async Task RefreshAsync(bool refreshLayout)
		{
			int operationId = await ShowModelAsync(operation => parserService.AnalyzeAsync(
				modelMetadata.ModelFilePath, items => UpdateDataItems(items, operation)));

			modelNodeService.RemoveObsoleteNodesAndLinks(operationId);

			if (refreshLayout)
			{
				modelNodeService.ResetLayout();
			}
		}


		private async Task<int> ShowModelAsync(Func<Operation, Task> parseFunctionAsync)
		{
			Operation operation = new Operation(currentId++);

			Timing t = Timing.Start();

			Task showTask = Task.Run(() => ShowModel(operation));

			Task parseTask = parseFunctionAsync(operation)
				.ContinueWith(_ => operation.Queue.CompleteAdding());

			await Task.WhenAll(showTask, parseTask);

			t.Log("Shown all");
			return operation.Id;
		}


		private static void UpdateDataItems(ModelItem item, Operation operation)
		{
			int priority = GetPriority(item, operation);

			operation.Queue.Enqueue(item, priority);
		}


		private void ShowModel(Operation operation)
		{
			while (operation.Queue.TryTake(out ModelItem item, -1))
			{
				Application.Current.Dispatcher.InvokeBackground(() =>
				{
					UpdateItem(item, operation.Id);

					for (int i = 0; i < BatchSize; i++)
					{
						if (!operation.Queue.TryTake(out item, 0))
						{
							break;
						}

						UpdateItem(item, operation.Id);
					}
				});
			}
		}


		private void UpdateItem(ModelItem item, int stamp)
		{
			if (item.Node != null)
			{
				modelNodeService.UpdateNode(item.Node, stamp);
			}

			if (item.Link != null)
			{
				modelLinkService.UpdateLink(item.Link, stamp);
			}
		}


		private static int GetPriority(ModelItem item, Operation operation)
		{
			if (item.Node != null)
			{
				return operation.GetPriority(item.Node.Name);
			}
			else
			{
				return Math.Max(
					operation.GetPriority(item.Link.Source),
					operation.GetPriority(item.Link.Target));
			}
		}


		private string GetDataFilePath() => Path.Combine(modelMetadata, "data.json");


		private class Operation
		{
			//private readonly ConcurrentDictionary<NodeName, int> parents = 
			//	new ConcurrentDictionary<NodeName, int>();

			public PriorityBlockingQueue<ModelItem> Queue { get; } = new PriorityBlockingQueue<ModelItem>(MaxPriority);

			public int Id { get; }

			public Operation(int stamp) => Id = stamp;


			public int GetPriority(NodeName name)
			{
				int priority = 0;

				foreach (char t in name.FullName)
				{
					if (t == '(')
					{
						break;
					}

					if (t == '.')
					{
						priority++;
					}

					if (priority >= MaxPriority - 1)
					{
						break;
					}
				}

				return priority;
			}
		}
	}
}