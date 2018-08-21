using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.ModelViewing.Private.DataHandling;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.ModelHandling.Core;
using Dependinator.Utils;
using Dependinator.Utils.Dependencies;
using Dependinator.Utils.ErrorHandling;
using Dependinator.Utils.Threading;
using Dependinator.Utils.UI;


namespace Dependinator.ModelViewing.Private.ModelHandling.Private
{
    [SingleInstance]
    internal class ModelPersistentHandler : IModelPersistentHandler
    {
        private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan Soon = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan Immediately = TimeSpan.Zero;
        private readonly IDataService dataService;
        private readonly ModelMetadata metadata;


        private readonly IModelDatabase modelDatabase;
        private readonly TaskThrottler saveToDiskThrottler = new TaskThrottler(1);

        private readonly ThrottleDispatcher triggerSaveThrottler = new ThrottleDispatcher();

        private bool isDataModified;
        private bool isSaveScheduled;
        private TaskCompletionSource<bool> saveInProgressTcs;


        public ModelPersistentHandler(
            IModelDatabase modelDatabase,
            IDataService dataService,
            ModelMetadata metadata)
        {
            this.modelDatabase = modelDatabase;
            this.dataService = dataService;
            this.metadata = metadata;

            modelDatabase.DataModified += OnDataModified;

            // Create an "already saved" progress tcs, in case a call is made to SaveAsync()
            saveInProgressTcs = new TaskCompletionSource<bool>();
            saveInProgressTcs.TrySetResult(true);
        }


        public Task SaveIfModifiedAsync()
        {
            if (isDataModified)
            {
                ScheduleSaveModel(Immediately);
            }

            // Only takes time if as save actually is in progress (resent not yes saved change)
            return saveInProgressTcs.Task;
        }


        public void TriggerDataModified() => TriggerDataModified(Soon);


        // Called when model data has been edited (node moved, line adjusted) 
        private void OnDataModified(object sender, EventArgs e) => TriggerDataModified(SaveInterval);


        private void TriggerDataModified(TimeSpan withinTime)
        {
            isDataModified = true;

            if (isSaveScheduled) return;

            isSaveScheduled = true;
            ScheduleSaveModel(withinTime);
        }


        private void ScheduleSaveModel(TimeSpan withinTime)
        {
            Log.Debug($"Model has changed, a save is scheduled within {withinTime}");
            saveInProgressTcs = new TaskCompletionSource<bool>();
            triggerSaveThrottler.Throttle(withinTime, SaveModelAsync, saveInProgressTcs);
        }


        private async void SaveModelAsync(object state)
        {
            Log.Debug("Data being saved ...");
            TaskCompletionSource<bool> tcs = (TaskCompletionSource<bool>)state;
            isSaveScheduled = false;

            if (isDataModified)
            {
                IReadOnlyList<IDataItem> items = await GetModelSnapshotAsync();

                await saveToDiskThrottler.Run(() => dataService.SaveAsync(metadata.ModelPaths, items));
            }

            tcs.TrySetResult(true);
            Log.Debug("Data saved ");
        }


        private async Task<IReadOnlyList<IDataItem>> GetModelSnapshotAsync()
        {
            // Taking a snapshot of all nodes and lines in the model. But since that might
            // take some time, it is done in batches to allow other activities. If changes to
            // the model occur while taking the snapshot, the current attempt will be canceled
            // and a new attempt will be retried.
            while (true)
            {
                isDataModified = false;

                Timing t = Timing.Start();
                M<IReadOnlyList<IDataItem>> items = await TryGetModelSnapshotAsync();

                if (items.IsOk)
                {
                    t.Log($"Got snapshot");
                    return items.Value;
                }

                Log.Debug("Model data changed while taking model snapshot, retrying");
                await Task.Delay(1000);
            }
        }


        private async Task<M<IReadOnlyList<IDataItem>>> TryGetModelSnapshotAsync()
        {
            try
            {
                List<IDataItem> items = new List<IDataItem>();
                int batchSize = 10000;
                int count = 0;

                // Take the snapshot in small batches to allow other activities and cancel if model
                // was changed due to those activities
                foreach (Node node in AllNodes(modelDatabase.Root))
                {
                    if (count++ % batchSize == 0)
                    {
                        // Allow some time for other activities
                        await Task.Delay(10);

                        // Stop if models was modified
                        if (isDataModified) return M.NoValue;
                    }

                    items.AddRange(Convert.ToDataItems(node));
                }

                return items;
            }
            catch (InvalidOperationException)
            {
                // Nodes, links or lines changed while iterating. It is very rare.
                // Just treating it as if some item where changed
                return M.NoValue;
            }
        }



        public static IEnumerable<Node> AllNodes(Node node)
        {
            Queue<Node> queue = new Queue<Node>();

            node.Children.OrderBy(c => c.Name.FullName).ForEach(queue.Enqueue);

            while (queue.Any())
            {
                Node descendent = queue.Dequeue();
                yield return descendent;

                descendent.Children.OrderBy(c => c.Name.FullName).ForEach(queue.Enqueue);
            }
        }
    }
}
