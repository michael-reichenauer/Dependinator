﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Dependinator.Common.MessageDialogs;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.Common.ProgressHandling;
using Dependinator.ModelViewing.Private.DataHandling;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.ItemsViewing;
using Dependinator.ModelViewing.Private.ModelHandling.Core;
using Dependinator.Utils;
using Dependinator.Utils.Dependencies;
using Dependinator.Utils.ErrorHandling;
using Dependinator.Utils.OsSystem;
using Dependinator.Utils.Threading;


namespace Dependinator.ModelViewing.Private.ModelHandling.Private
{
	[SingleInstance]
	internal class ModelHandlingService : IModelHandlingService, IModelNotifications
	{
		private static readonly int BatchSize = 100;

		private readonly IDataService dataService;
		private readonly IDataMonitorService dataMonitorService;

		private readonly IRecentModelsService recentModelsService;
		private readonly IMessage message;
		private readonly IProgressService progress;
		private readonly ICmd cmd;
		private readonly Func<OpenModelViewModel> openModelViewModelProvider;

		private readonly IModelService modelService;
		private readonly ModelMetadata modelMetadata;

		
		private bool isShowingOpenModel = false;
		private bool isWorking = false;

		public ModelHandlingService(
			IDataService dataService,
			IDataMonitorService dataMonitorService,
			Func<OpenModelViewModel> openModelViewModelProvider,
			IModelService modelService,
			ModelMetadata modelMetadata,
			IRecentModelsService recentModelsService,
			IMessage message,
			IProgressService progress,
			ICmd cmd)
		{
			this.dataService = dataService;
			this.dataMonitorService = dataMonitorService;

			this.openModelViewModelProvider = openModelViewModelProvider;

			this.modelService = modelService;
			this.modelMetadata = modelMetadata;
			this.recentModelsService = recentModelsService;
			this.message = message;
			this.progress = progress;
			this.cmd = cmd;

			dataMonitorService.ChangedOccurred += ChangedFiles;
		}


		public event EventHandler ModelUpdated;

		public Node Root => modelService.Root;

		public void SetRootCanvas(ItemsCanvas rootCanvas) => Root.ItemsCanvas = rootCanvas;


		public Task ManualRefreshAsync(bool refreshLayout = false) => RefreshAsync(refreshLayout);


		public async Task LoadAsync()
		{
			using (progress.ShowBusy())
			{
				isWorking = true;
				Log.Debug($"Metadata model: {modelMetadata.ModelFilePath} {DateTime.Now}");
				string dataFilePath = GetDataFilePath();

				Root.ItemsCanvas.IsZoomAndMoveEnabled = true;

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
				}
				else if (File.Exists(modelMetadata.ModelFilePath))
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
					Root.ItemsCanvas.SetRootScale(1);
					Root.ItemsCanvas.IsZoomAndMoveEnabled = false;
					Root.ItemsCanvas.UpdateAndNotifyAll(true);

					Root.ItemsCanvas.AddItem(openModelViewModelProvider());
				}
				else
				{
					isShowingOpenModel = false;
					Root.ItemsCanvas.IsZoomAndMoveEnabled = true;
					UpdateLines(Root);
					recentModelsService.AddModelPaths(modelMetadata.ModelFilePath);
					modelService.SetLayoutDone();
				}

				GC.Collect();
				isWorking = false;

				dataMonitorService.Start(modelMetadata.ModelFilePath);
			}
		}


		public async Task RefreshAsync(bool isClean)
		{
			if (isWorking)
			{
				return;
			}

			using (progress.ShowBusy())
			{
				if (isClean)
				{
					string dataFilePath = GetDataFilePath();

					if (File.Exists(dataFilePath))
					{
						File.Delete(dataFilePath);
					}

					Root.ItemsCanvas.SetRootScale(2);

					modelService.RemoveAll();
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

					modelService.SetLayoutDone();
					GC.Collect();
					isWorking = false;
					return;
				}

				modelService.RemoveObsoleteNodesAndLinks(operationId.Value);
				modelService.SetLayoutDone();
				GC.Collect();
				isWorking = false;
			}

			ModelUpdated?.Invoke(this, EventArgs.Empty);
		}


		public IReadOnlyList<NodeName> GetHiddenNodeNames()
		{
			return modelService.GetHiddenNodeNames();
		}


		public void ShowHiddenNode(NodeName nodeName)
		{
			modelService.ShowHiddenNode(nodeName);
		}



		private async void ChangedFiles(object sender, EventArgs e)
		{
			if (!isWorking)
			{
				await RefreshAsync(false);
			}
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
				.Where(child => child.IsShowing)
				.ForEach(UpdateLines);
		}


		public void Close()
		{
			dataMonitorService.Stop();
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
			//IReadOnlyList<Node> nodes = Root.Descendents().ToList();
			//t.Log($"Saving {nodes.Count} nodes");

			IReadOnlyList<IDataItem> items = Convert.ToDataItems(Root.Descendents()).ToList();
			t.Log($"Converted {items.Count} items");

			string dataFilePath = GetDataFilePath();

			dataService.SaveData(items, dataFilePath);
			t.Log($"Saved {items.Count} items");
		}



		private async Task<R<int>> ShowModelAsync(Func<Operation, Task<R>> parseFunctionAsync)
		{
			Operation operation = new Operation();

			Timing t = Timing.Start();

			Task showTask = Task.Run(() => ShowModel(operation));

			Task<R> parseTask = parseFunctionAsync(operation);

			Task completeTask = parseTask.ContinueWith(_ => operation.Queue.CompleteAdding());

			await Task.WhenAll(showTask, parseTask, completeTask);

			Root.ItemsCanvas.UpdateAll();

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
			while (operation.Queue.TryTake(out IDataItem item, Timeout.Infinite))
			{
				Application.Current.Dispatcher.InvokeBackground(() =>
				{
					modelService.AddOrUpdateItem(item, operation.Id);

					for (int i = 0; i < BatchSize; i++)
					{
						if (!operation.Queue.TryTake(out item, 0))
						{
							break;
						}

						modelService.AddOrUpdateItem(item, operation.Id);
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


			while (queue.TryTake(out IDataItem item, Timeout.Infinite))
			{
				Application.Current.Dispatcher.InvokeBackground(() =>
				{
					modelService.AddOrUpdateItem(item, operation.Id);

					for (int i = 0; i < BatchSize; i++)
					{
						if (!queue.TryTake(out item, 0))
						{
							break;
						}

						modelService.AddOrUpdateItem(item, operation.Id);
					}
				});
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
			private static int currentId;
			
			public BlockingCollection<IDataItem> Queue { get; } = new BlockingCollection<IDataItem>();

			public int Id { get; }

			public Operation() => Id = Interlocked.Increment(ref currentId);
		}
	}
}
