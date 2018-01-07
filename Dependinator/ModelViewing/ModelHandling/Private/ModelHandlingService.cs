using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Dependinator.Common;
using Dependinator.Common.MessageDialogs;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.ModelHandling.Private.ModelParsing;
using Dependinator.ModelViewing.ModelHandling.Private.ModelPersistence;
using Dependinator.ModelViewing.Open;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	[SingleInstance]
	internal class ModelHandlingService : IModelHandlingService
	{
		private static readonly int MaxPriority = 10;
		private static readonly int BatchSize = 100;

		private readonly IParserService parserService;
		private readonly IPersistenceService persistenceService;
		private readonly IModelNodeService modelNodeService;
		private readonly IModelLinkService modelLinkService;
		private readonly IModelLineService modelLineService;
		private readonly IRecentModelsService recentModelsService;
		private readonly IMessage message;
		private readonly Func<OpenModelViewModel> openModelViewModelProvider;

		private readonly Model model;
		private readonly ModelMetadata modelMetadata;

		private int currentId;
		private bool isShowingOpenModel = false;
		private bool isWorking = false;

		public ModelHandlingService(
			IParserService parserService,
			IPersistenceService persistenceService,
			IModelNodeService modelNodeService,
			IModelLinkService modelLinkService,
			IModelLineService modelLineService,
			Func<OpenModelViewModel> openModelViewModelProvider,
			Model model,
			ModelMetadata modelMetadata,
			IRecentModelsService recentModelsService,
			IMessage message)
		{
			this.parserService = parserService;
			this.persistenceService = persistenceService;
			this.modelNodeService = modelNodeService;
			this.modelLinkService = modelLinkService;
			this.modelLineService = modelLineService;
			this.openModelViewModelProvider = openModelViewModelProvider;

			this.model = model;
			this.modelMetadata = modelMetadata;
			this.recentModelsService = recentModelsService;
			this.message = message;
		}


		public Node Root => model.Root;

		public void SetRootCanvas(ItemsCanvas rootCanvas) => Root.View.ItemsCanvas = rootCanvas;


		public void AddLineViewModel(Line line) => modelLineService.AddLineViewModel(line);


		public async Task LoadAsync()
		{
			isWorking = true;
			Log.Debug($"Metadata model: {modelMetadata.ModelFilePath} {DateTime.Now}");
			string dataFilePath = GetDataFilePath();

			ClearAll();
			Root.View.ItemsCanvas.IsZoomAndMoveEnabled = true;

			if (File.Exists(dataFilePath))
			{
				await ShowModelAsync(operation => persistenceService.TryDeserialize(
					dataFilePath, items => UpdateDataItems(items, operation)));
			}
			else
			if (File.Exists(modelMetadata.ModelFilePath))
			{
				await ShowModelAsync(operation => parserService.ParseAsync(
					modelMetadata.ModelFilePath, items => UpdateDataItems(items, operation)));
			}

			if (!Root.Children.Any())
			{
				if (!modelMetadata.IsDefault)
				{
					message.ShowWarning($"Could not load model from:\n{modelMetadata.ModelFilePath}");
				}

				if (File.Exists(dataFilePath))
				{
					File.Delete(dataFilePath);
				}

				isShowingOpenModel = true;
				modelMetadata.SetDefault();
				Root.View.ItemsCanvas.SetRootScale(1);
				//Root.ItemsCanvas.SetMoveOffset(new Point(0, 0));
				Root.View.ItemsCanvas.IsZoomAndMoveEnabled = false;

				Root.View.ItemsCanvas.AddItem(openModelViewModelProvider());
			}
			else
			{
				isShowingOpenModel = false;
				Root.View.ItemsCanvas.IsZoomAndMoveEnabled = true;
				UpdateLines(Root);
				recentModelsService.AddModelPaths(modelMetadata.ModelFilePath);
				modelNodeService.SetLayoutDone();
			}

			GC.Collect();
			isWorking = false;
		}


		public IReadOnlyList<NodeName> GetHiddenNodeNames()
		{
			return modelNodeService.GetHiddenNodeNames();
		}


		public void ShowHiddenNode(NodeName nodeName)
		{
			modelNodeService.ShowHiddenNode(nodeName);
		}


		private static void UpdateLines(Node node)
		{
			node.SourceLines
				.Where(line => line.View.IsShowing)
				.ForEach(line => line.View.ViewModel.NotifyAll());

			node.Children
				.Where(child => child.View.IsShowing)
				.ForEach(UpdateLines);
		}



		public void ClearAll() => modelNodeService.RemoveAll();


		public async Task SaveAsync()
		{
			if (isWorking)
			{
				return;
			}

			if (isShowingOpenModel)
			{
				// Nothing to save
				return;
			}

			Timing t = Timing.Start();
			IReadOnlyList<Node> nodes = Root.DescendentsBreadth().ToList();
			t.Log($"Saving {nodes} nodes");

			IReadOnlyList<IModelItem> items = Convert.ToDataItems(nodes);
			t.Log($"Saving {items} items");

			string dataFilePath = GetDataFilePath();
			await persistenceService.SerializeAsync(items, dataFilePath);
			t.Log($"Saved {items} items");
		}


		public void Save()
		{
			if (isWorking)
			{
				return;
			}

			if (isShowingOpenModel)
			{
				// Nothing to save
				return;
			}

			Timing t = Timing.Start();
			IReadOnlyList<Node> nodes = Root.DescendentsBreadth().ToList();
			t.Log($"Saving {nodes.Count} nodes");

			IReadOnlyList<IModelItem> items = Convert.ToDataItems(nodes);
			t.Log($"Saving {items.Count} items");

			string dataFilePath = GetDataFilePath();

			persistenceService.Serialize(items, dataFilePath);
			t.Log($"Saved {items.Count} items");
		}


		public async Task RefreshAsync(bool isClean)
		{
			if (isClean)
			{
				string dataFilePath = GetDataFilePath();

				if (File.Exists(dataFilePath))
				{
					File.Delete(dataFilePath);
				}

				await LoadAsync();
				return;
			}

			isWorking = true;
			int operationId = await ShowModelAsync(operation => parserService.ParseAsync(
				modelMetadata.ModelFilePath, items => UpdateDataItems(items, operation)));

			modelNodeService.RemoveObsoleteNodesAndLinks(operationId);
			modelNodeService.SetLayoutDone();
			GC.Collect();
			isWorking = false;
		}


		private async Task<int> ShowModelAsync(Func<Operation, Task> parseFunctionAsync)
		{
			Operation operation = new Operation(currentId++);

			Timing t = Timing.Start();

			Task showTask = Task.Run(() => ShowModel(operation));
			Root.View.ItemsCanvas.UpdateAll();

			Task parseTask = parseFunctionAsync(operation)
				.ContinueWith(_ => operation.Queue.CompleteAdding());

			await Task.WhenAll(showTask, parseTask);


			t.Log("Shown all");
			return operation.Id;
		}


		private static void UpdateDataItems(IModelItem item, Operation operation)
		{
			operation.Queue.Enqueue(item, 0);
		}


		private void ShowModel(Operation operation)
		{
			while (operation.Queue.TryTake(out IModelItem item, -1))
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


			PriorityBlockingQueue<IModelItem> queue = new PriorityBlockingQueue<IModelItem>(MaxPriority);

			Application.Current.Dispatcher.InvokeBackground(() =>
			{
				model.GetAllQueuedNodes().ForEach(node => queue.Enqueue(node, 0));
				queue.CompleteAdding();
			});

			while (queue.TryTake(out IModelItem item, -1))
			{
				Application.Current.Dispatcher.InvokeBackground(() =>
				{
					UpdateItem(item, operation.Id);

					for (int i = 0; i < BatchSize; i++)
					{
						if (!queue.TryTake(out item, 0))
						{
							break;
						}

						UpdateItem(item, operation.Id);
					}
				});
			}
		}


		private void UpdateItem(IModelItem item, int stamp)
		{
			if (item is ModelNode modelNode)
			{
				modelNodeService.UpdateNode(modelNode, stamp);
			}

			if (item is ModelLine modelLine)
			{
				modelLineService.UpdateLine(modelLine, stamp);
			}

			if (item is ModelLink modelLink)
			{
				modelLinkService.UpdateLink(modelLink, stamp);
			}
		}




		private string GetDataFilePath()
		{
			string dataJson = $"{Path.GetFileName(modelMetadata.ModelFilePath)}.dn.json";
			string dataFilePath = Path.Combine(modelMetadata, dataJson);
			return dataFilePath;
		}


		private class Operation
		{
			public PriorityBlockingQueue<IModelItem> Queue { get; } =
				new PriorityBlockingQueue<IModelItem>(MaxPriority);

			public int Id { get; }

			public Operation(int stamp) => Id = stamp;
		}
	}
}