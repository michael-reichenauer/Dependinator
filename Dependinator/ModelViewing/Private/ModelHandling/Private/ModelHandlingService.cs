using System;
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
using Dependinator.Common.SettingsHandling;
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
    internal class ModelHandlingService : IModelHandlingService
    {
        private static readonly int BatchSize = 100;
        private readonly ICmd cmd;


        private readonly IDataService dataService;
        private readonly IMessage message;
        private readonly ModelMetadata modelMetadata;
        private readonly IModelPersistentHandler modelPersistentHandler;

        private readonly IModelService modelService;
        private readonly Func<OpenModelViewModel> openModelViewModelProvider;
        private readonly IProgressService progress;

        private readonly IRecentModelsService recentModelsService;
        private readonly IModelNotificationService modelNotificationService;


        private bool isShowingOpenModel;
        private bool isWorking;


        public ModelHandlingService(
            IDataService dataService,
            IModelPersistentHandler modelPersistentHandler,
            Func<OpenModelViewModel> openModelViewModelProvider,
            IModelService modelService,
            ModelMetadata modelMetadata,
            IRecentModelsService recentModelsService,
            IModelNotificationService modelNotificationService,
            IMessage message,
            IProgressService progress,
            ICmd cmd)
        {
            this.dataService = dataService;
            this.modelPersistentHandler = modelPersistentHandler;

            this.openModelViewModelProvider = openModelViewModelProvider;

            this.modelService = modelService;
            this.modelMetadata = modelMetadata;
            this.recentModelsService = recentModelsService;
            this.modelNotificationService = modelNotificationService;
            this.message = message;
            this.progress = progress;
            this.cmd = cmd;

            dataService.DataChanged += DataChangedFiles;
        }


        public Node Root => modelService.Root;

        public void SetRootCanvas(ItemsCanvas rootCanvas) => Root.ItemsCanvas = rootCanvas;


        public async Task LoadAsync()
        {
            using (progress.ShowBusy())
            {
                isWorking = true;
                Log.Debug($"Metadata model: {modelMetadata.ModelPaths} {DateTime.Now}");

                if (modelMetadata.IsDefault)
                {
                    isShowingOpenModel = true;
                    modelMetadata.SetDefault();
                    Root.ItemsCanvas.SetRootScale(1);
                    Root.ItemsCanvas.SetRootOffset(new Point(-32, 50));
                    Root.ItemsCanvas.IsZoomAndMoveEnabled = false;
                    Root.ItemsCanvas.UpdateAndNotifyAll(true);

                    Root.ItemsCanvas.AddItem(openModelViewModelProvider());
                    return;
                }

                if (!File.Exists(modelMetadata.ModelPaths.ModelPath))
                {
                    message.ShowWarning($"Model not found:\n{modelMetadata.ModelFilePath}");
                    recentModelsService.RemoveModelPath(modelMetadata.ModelFilePath);
                    string targetPath = ProgramInfo.GetInstallFilePath();
                    cmd.Start(targetPath, "");
                    Application.Current.Shutdown(0);
                    return;
                }


                Root.ItemsCanvas.IsZoomAndMoveEnabled = true;

                M result = await TryShowModelAsync();

                if (result.IsFaulted)
                {
                    message.ShowWarning(result.ErrorMessage);
                    string targetPath = ProgramInfo.GetInstallFilePath();
                    cmd.Start(targetPath, "");
                    Application.Current.Shutdown(0);
                    return;
                }

                UpdateLines(Root);
                recentModelsService.AddModelPaths(modelMetadata.ModelFilePath);
                modelService.SetLayoutDone();


                GC.Collect();
                isWorking = false;
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
                isWorking = true;
                M<int> operationId = await TryShowRefreshedModelAsync();

                if (operationId.IsFaulted)
                {
                    message.ShowWarning(operationId.ErrorMessage);

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
                modelPersistentHandler.TriggerDataModified();
                GC.Collect();
                isWorking = false;
            }

            modelNotificationService.TriggerNotification();
        }


        public IReadOnlyList<NodeName> GetHiddenNodeNames()
        {
            return modelService.GetHiddenNodeNames();
        }


        public void ShowHiddenNode(NodeName nodeName)
        {
            modelService.ShowHiddenNode(nodeName);
        }


        public async Task CloseAsync()
        {
            dataService.DataChanged -= DataChangedFiles;

            if (isWorking)
            {
                return;
            }

            if (isShowingOpenModel)
            {
                // Nothing to save
                return;
            }

            await modelPersistentHandler.SaveIfModifiedAsync();
        }


        private void DataChangedFiles(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.InvokeBackground(async () =>
            {
                if (!isWorking)
                {
                    await RefreshAsync(false);
                }
            });
        }


        private async Task<M> TryShowModelAsync()
        {
            M cacheResult = await ShowModelAsync(operation => dataService.TryReadCacheAsync(
                modelMetadata.ModelPaths, items => UpdateDataItems(items, operation)));

            if (cacheResult.IsOk)
            {
                dataService.TriggerDataChangedIfDataNewerThanCache(modelMetadata.ModelPaths);
                return M.Ok;
            }


            modelService.Root.ItemsCanvas.SetRootScale(WorkFolderSettings.DefaultScale);
            modelService.Root.ItemsCanvas.SetRootOffset(WorkFolderSettings.DefaultOffset);

            modelService.Root.ItemsCanvas.ZoomableCanvas.Scale = WorkFolderSettings.DefaultScale;
            modelService.Root.ItemsCanvas.ZoomableCanvas.Offset = WorkFolderSettings.DefaultOffset;

            var savedItems = await dataService.TryReadSaveAsync(modelMetadata.ModelPaths);
            if (savedItems.IsOk)
            {
                modelService.SetSaveData(savedItems.Value);
            }
            else
            {
                modelService.SetSaveData(new List<IDataItem>());
            }

            M freshResult = await ShowModelAsync(operation => dataService.TryReadFreshAsync(
                modelMetadata.ModelPaths, items => UpdateDataItems(items, operation)));

            if (freshResult.IsOk)
            {
                modelPersistentHandler.TriggerDataModified();
            }

            return freshResult;
        }


        private Task<M<int>> TryShowRefreshedModelAsync()
        {
            return ShowModelAsync(operation => dataService.TryReadFreshAsync(
                modelMetadata.ModelPaths, items => UpdateDataItems(items, operation)));
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


        private async Task<M<int>> ShowModelAsync(Func<Operation, Task<M>> parseFunctionAsync)
        {
            Operation operation = new Operation();

            Timing t = Timing.Start();

            Task showTask = Task.Run(() => ShowModel(operation));

            Task<M> parseTask = parseFunctionAsync(operation);

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


        private class Operation
        {
            private static int currentId;

            public Operation() => Id = Interlocked.Increment(ref currentId);

            public BlockingCollection<IDataItem> Queue { get; } = new BlockingCollection<IDataItem>();

            public int Id { get; }
        }
    }
}
