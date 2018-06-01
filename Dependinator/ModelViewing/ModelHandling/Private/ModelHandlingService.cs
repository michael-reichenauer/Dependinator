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
using Dependinator.Common.SettingsHandling;
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
		private readonly ICmd cmd;
		private readonly Func<OpenModelViewModel> openModelViewModelProvider;

		private readonly IModelService modelService;
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
			IModelService modelService,
			ModelMetadata modelMetadata,
			IRecentModelsService recentModelsService,
			IMessage message,
			ICmd cmd)
		{
			this.parserService = parserService;
			this.persistenceService = persistenceService;
			this.modelNodeService = modelNodeService;
			this.modelLinkService = modelLinkService;
			this.modelLineService = modelLineService;
			this.openModelViewModelProvider = openModelViewModelProvider;

			this.modelService = modelService;
			this.modelMetadata = modelMetadata;
			this.recentModelsService = recentModelsService;
			this.message = message;
			this.cmd = cmd;
		}


		public Node Root => modelService.Root;

		public void SetRootCanvas(ItemsCanvas rootCanvas) => Root.View.ItemsCanvas = rootCanvas;


		public async Task LoadAsync()
		{
			isWorking = true;
			Log.Debug($"Metadata model: {modelMetadata.ModelFilePath} {DateTime.Now}");
			string dataFilePath = GetDataFilePath();

			ClearAll();
			Root.View.ItemsCanvas.IsZoomAndMoveEnabled = true;

			if (File.Exists(dataFilePath))
			{
				R result = await ShowModelAsync(operation => persistenceService.TryDeserialize(
					dataFilePath, items => UpdateDataItems(items, operation)));
				if (result.IsFaulted)
				{
					message.ShowWarning(result.Message);
					string targetPath = ProgramInfo.GetInstallFilePath();
					cmd.Start(targetPath, "");
					Application.Current.Shutdown(0);
					return;
				}

				await RefreshAsync(false);
			}
			else
			if (File.Exists(modelMetadata.ModelFilePath))
			{
				R result = await ShowModelAsync(operation => parserService.ParseAsync(
					modelMetadata.ModelFilePath, items => UpdateDataItems(items, operation)));
				if (result.IsFaulted)
				{
					message.ShowWarning(result.Message);
					string targetPath = ProgramInfo.GetInstallFilePath();
					cmd.Start(targetPath, "");
					Application.Current.Shutdown(0);
					return;
				}
			}

			if (!modelMetadata.IsDefault && !File.Exists(dataFilePath) && !File.Exists(modelMetadata.ModelFilePath))
			{
				message.ShowWarning($"Model not found:\n{modelMetadata.ModelFilePath}");
				recentModelsService.RemoveModelPath(modelMetadata.ModelFilePath);
				string targetPath = ProgramInfo.GetInstallFilePath();
				cmd.Start(targetPath, "");
				Application.Current.Shutdown(0);
				return;
			}

			if (!Root.Children.Any())
			{
				if (File.Exists(dataFilePath))
				{
					File.Delete(dataFilePath);
				}

				isShowingOpenModel = true;
				modelMetadata.SetDefault();
				Root.View.ItemsCanvas.SetRootScale(1);
				Root.View.ItemsCanvas.IsZoomAndMoveEnabled = false;
				//Root.View.ItemsCanvas.ZoomRootNode(1, new Point(0, 0));
				Root.View.ItemsCanvas.UpdateAndNotifyAll();

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
			R<int> operationId = await ShowModelAsync(operation => parserService.ParseAsync(
				modelMetadata.ModelFilePath, items => UpdateDataItems(items, operation)));

			if (operationId.IsFaulted)
			{
				message.ShowWarning(operationId.Message);

				if (!File.Exists(modelMetadata.ModelFilePath))
				{
					recentModelsService.RemoveModelPath(modelMetadata.ModelFilePath);
				}

				modelNodeService.SetLayoutDone();
				GC.Collect();
				isWorking = false;
				return;
			}

			modelNodeService.RemoveObsoleteNodesAndLinks(operationId.Value);
			modelNodeService.SetLayoutDone();
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


		private async Task<R<int>> ShowModelAsync(Func<Operation, Task<R>> parseFunctionAsync)
		{
			Operation operation = new Operation(currentId++);

			Timing t = Timing.Start();

			Task showTask = Task.Run(() => ShowModel(operation));
			Root.View.ItemsCanvas.UpdateAll();

			Task<R> parseTask = parseFunctionAsync(operation);

			Task completeTask = parseTask.ContinueWith(_ => operation.Queue.CompleteAdding());

			await Task.WhenAll(showTask, parseTask, completeTask);

			if (parseTask.Result.IsFaulted)
			{
				return parseTask.Result.Error;
			}

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
				modelService.GetAllQueuedNodes().ForEach(node => queue.Enqueue(node, 0));
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
			switch (item)
			{
				case ModelLine line:
					modelLineService.UpdateLine(line, stamp);
					break;
				case ModelLink link:
					modelLinkService.UpdateLink(link, stamp);
					break;
				case ModelNode node:
					modelNodeService.UpdateNode(node, stamp);
					break;
				default:
					throw Asserter.FailFast($"Unknown item type {item}");
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