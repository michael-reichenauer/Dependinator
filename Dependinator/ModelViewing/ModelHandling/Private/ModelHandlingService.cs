using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Dependinator.Common.MessageDialogs;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.ModelViewing.DataHandling;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.Open;
using Dependinator.Utils;
using Dependinator.Utils.Dependencies;
using Dependinator.Utils.ErrorHandling;
using Dependinator.Utils.OsSystem;
using Dependinator.Utils.Threading;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	[SingleInstance]
	internal class ModelHandlingService : IModelHandlingService
	{
		private static readonly int BatchSize = 100;

		private readonly IDataService dataService;
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
			IDataService dataService,
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
			this.dataService = dataService;
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

			Root.View.ItemsCanvas.IsZoomAndMoveEnabled = true;

			if (File.Exists(dataFilePath))
			{
				R result = await TryShowSavedModelAsync(dataFilePath);
				if (result.Error.Exception is NotSupportedException)
				{
					File.Delete(dataFilePath);
					await LoadAsync();
					return;
				}

				if (result.IsFaulted)
				{
					message.ShowWarning(result.Message);
					string targetPath = ProgramInfo.GetInstallFilePath();
					cmd.Start(targetPath, "");
					Application.Current.Shutdown(0);
					return;
				}

				//await RefreshAsync(false);
			}
			else
			if (File.Exists(modelMetadata.ModelFilePath))
			{
				R result = await ShowParsedModelAsync();
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

				modelNodeService.RemoveAll();
				await LoadAsync();
				return;
			}

			isWorking = true;
			R<int> operationId = await ShowParsedModelAsync();

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



		private Task<R<int>> ShowParsedModelAsync()
		{
			return ShowModelAsync(operation => dataService.ParseAsync(
				modelMetadata.ModelFilePath, items => UpdateDataItems(items, operation)));
		}


		private Task<R<int>> TryShowSavedModelAsync(string dataFilePath)
		{
			return ShowModelAsync(operation => dataService.TryReadSavedDataAsync(
				dataFilePath, items => UpdateDataItems(items, operation)));
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
			IReadOnlyList<Node> nodes = Root.Descendents().ToList();
			t.Log($"Saving {nodes.Count} nodes");

			IReadOnlyList<IDataItem> items = Convert.ToDataItems(nodes);
			t.Log($"Saving {items.Count} items");

			string dataFilePath = GetDataFilePath();

			dataService.SaveData(items, dataFilePath);
			t.Log($"Saved {items.Count} items");
		}



		private async Task<R<int>> ShowModelAsync(Func<Operation, Task<R>> parseFunctionAsync)
		{
			Operation operation = new Operation(currentId++);

			Timing t = Timing.Start();

			Task showTask = Task.Run(() => ShowModel(operation));


			Task<R> parseTask = parseFunctionAsync(operation);

			Task completeTask = parseTask.ContinueWith(_ => operation.Queue.CompleteAdding());

			await Task.WhenAll(showTask, parseTask, completeTask);

			Root.View.ItemsCanvas.UpdateAll();

			if (parseTask.Result.IsFaulted)
			{
				return parseTask.Result.Error;
			}

			t.Log("Shown all");
			return operation.Id;
		}


		private static void UpdateDataItems(IDataItem item, Operation operation)
		{
			operation.Queue.Add(item);
		}


		private void ShowModel(Operation operation)
		{
			while (operation.Queue.TryTake(out IDataItem item, -1))
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


			BlockingCollection<IDataItem> queue = new BlockingCollection<IDataItem>();

			Application.Current.Dispatcher.InvokeBackground(() =>
			{
				IReadOnlyList<DataNode> allQueuedNodes = modelService.GetAllQueuedNodes();
				allQueuedNodes.ForEach(node => queue.Add(node));
				queue.CompleteAdding();
			});

			while (queue.TryTake(out IDataItem item, -1))
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


		private void UpdateItem(IDataItem item, int stamp)
		{
			switch (item)
			{
				case DataLine line:
					modelLineService.UpdateLine(line, stamp);
					break;
				case DataLink link:
					modelLinkService.UpdateLink(link, stamp);
					break;
				case DataNode node:
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
			public BlockingCollection<IDataItem> Queue { get; } =
				new BlockingCollection<IDataItem>();

			public int Id { get; }

			public Operation(int stamp) => Id = stamp;
		}
	}
}