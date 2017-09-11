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
		private static readonly int MaxPriority = 10;
		private static readonly int BatchSize = 1000;

		private readonly IParserService parserService;
		private readonly INodeService nodeService;
		private readonly ILinkService linkService;
		private readonly IRecentModelsService recentModelsService;
		private readonly Func<OpenModelViewModel> openModelViewModelProvider;

		private readonly Model model;
		private readonly ModelMetadata modelMetadata;

		private int currentId;
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

			ClearAll();

			if (File.Exists(dataFilePath))
			{
				await ShowModelAsync(operation => parserService.TryDeserialize(
					dataFilePath, items => UpdateDataItems(items, operation)));
			}
			else if (File.Exists(modelMetadata.ModelFilePath))
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
			int operationId = await ShowModelAsync(operation => parserService.AnalyzeAsync(
				modelMetadata.ModelFilePath, items => UpdateDataItems(items, operation)));

			nodeService.RemoveObsoleteNodesAndLinks(operationId);

			if (refreshLayout)
			{
				nodeService.ResetLayout();
			}
		}

		
		private async Task<int> ShowModelAsync(Func<Operation, Task> parseFunctionAsync)
		{
			Operation operation = new Operation(currentId++);

			Timing t = Timing.Start();

			Task showTask = Task.Run(() => ShowModel(operation));

			Task parseTask = parseFunctionAsync(operation)
				.ContinueWith(_ => operation.BlockingQueue.CompleteAdding());

			await Task.WhenAll(showTask, parseTask);

			t.Log("Shown all");
			return operation.Id;
		}


		private static void UpdateDataItems(IEnumerable<ModelItem> items, Operation operation)
		{
			foreach (ModelItem item in items)
			{
				int priority = GetPriority(item);

				operation.BlockingQueue.Enqueue(item, priority);
			}
		}


		private void ShowModel(Operation operation)
		{
			while (operation.BlockingQueue.TryTake(out ModelItem item, -1))
			{
				Application.Current.Dispatcher.InvokeBackground(() =>
				{
					UpdateItem(item, operation.Id);

					for (int i = 0; i < BatchSize; i++)
					{
						if (!operation.BlockingQueue.TryTake(out item, 0))
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
				nodeService.UpdateNode(item.Node, stamp);
			}

			if (item.Link != null)
			{
				linkService.UpdateLink(item.Link, stamp);
			}

		}


		private static int GetPriority(ModelItem item)
		{
			if (item.Node != null)
			{
				return GetPriority(item.Node.Name);
			}
			else
			{
				return Math.Max(GetPriority(item.Link.Source), GetPriority(item.Link.Target));
			}
		}


		private static int GetPriority(string name)
		{
			int priority = 0;

			foreach (char t in name)
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


		private string GetDataFilePath() => Path.Combine(modelMetadata, "data.json");


		private class Operation
		{
			public PriorityBlockingQueue<ModelItem> BlockingQueue { get; } = new PriorityBlockingQueue<ModelItem>(MaxPriority);

			public int Id { get; }
			
			public Operation(int stamp) => Id = stamp;
		}
	}
}