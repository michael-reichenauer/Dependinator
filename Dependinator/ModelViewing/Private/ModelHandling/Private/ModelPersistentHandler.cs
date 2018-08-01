using System;
using System.Collections.Generic;
using System.IO;
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
        private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan Immediately = TimeSpan.Zero;
        private readonly IDataService dataService;
        private readonly ModelMetadata metadata;

        private readonly IModelDatabase modelDatabase;
        private readonly TaskThrottler saveToDiskThrottler = new TaskThrottler(1);

        private readonly ThrottleDispatcher triggerSaveThrottler = new ThrottleDispatcher();

        private bool isDataModified;
        private bool isSaveTriggered;
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


        public bool IsChangeMonitored
        {
            get => modelDatabase.IsChangeMonitored;
            set => modelDatabase.IsChangeMonitored = true;
            //set => modelDatabase.IsChangeMonitored = value;
        }


        public Task SaveAsync()
        {
            if (isDataModified)
            {
                TriggerSave(Immediately);
            }

            // Only takes time if as save actually is in progress (resent not yes saved change)
            return saveInProgressTcs.Task;
        }


        public void TriggerDataModified() => OnDataModified(this, EventArgs.Empty);


        private void OnDataModified(object sender, EventArgs e)
        {
            isDataModified = true;

            if (!isSaveTriggered)
            {
                isSaveTriggered = true;
                TriggerSave(SaveInterval);
            }
        }


        private void TriggerSave(TimeSpan withinTime)
        {
            Log.Warn("Model save is triggered");
            saveInProgressTcs = new TaskCompletionSource<bool>();
            triggerSaveThrottler.Throttle(withinTime, SaveModelAsync, saveInProgressTcs);
        }


        private async void SaveModelAsync(object state)
        {
            Log.Warn("Data being saved ...");
            TaskCompletionSource<bool> tcs = (TaskCompletionSource<bool>)state;
            isSaveTriggered = false;

            if (isDataModified)
            {
                IReadOnlyList<IDataItem> items = await GetModelSnapshotAsync();

                await saveToDiskThrottler.Run(() => SaveModelAsync(items));
            }

            tcs.TrySetResult(true);
            Log.Warn("Data saved ");
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
                R<IReadOnlyList<IDataItem>> items = await TryGetModelSnapshotAsync();

                if (items.IsOk)
                {
                    t.Log($"Got snapshot");
                    return items.Value;
                }

                Log.Debug("Model data changed while taking model snapshot, retrying");
                await Task.Delay(1000);
            }
        }


        private async Task<R<IReadOnlyList<IDataItem>>> TryGetModelSnapshotAsync()
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
                        if (isDataModified) return R.NoValue;
                    }

                    items.AddRange(ToDataItems(node));
                }

                return items;
            }
            catch (InvalidOperationException)
            {
                // Nodes, links or lines changed while iterating. It is very rare.
                // Just treating it as if some item where changed
                return R.NoValue;
            }
        }


        private async Task SaveModelAsync(IReadOnlyList<IDataItem> items)
        {
            string saveFilePath = GetSaveFilePath();
            string cacheFilePath = GetCacheFilePath();

            await Task.Run(() =>
            {
                Timing t = Timing.Start();
                IReadOnlyList<IDataItem> saveItems = GetSaveItems(items);

                dataService.SaveData(saveItems, saveFilePath);
                t.Log($"Save {saveItems.Count} items");

                IReadOnlyList<IDataItem> cacheItems = GetCacheItems(items);
                dataService.CacheData(cacheItems, cacheFilePath);
                t.Log($"Cache {cacheItems.Count} items");
            });
        }


        private static List<IDataItem> GetSaveItems(IReadOnlyList<IDataItem> items)
        {
            List<IDataItem> saveItems = new List<IDataItem>();
            foreach (IDataItem dataItem in items)
            {
                if (dataItem is DataNode dataNode)
                {
                    saveItems.Add(dataNode);
                }

                if (dataItem is DataLine dataLine)
                {
                    if (dataLine.Points.Count > 2)
                    {
                        saveItems.Add(dataLine);
                    }
                }
            }

            return saveItems;
        }

        //private static List<IDataItem> GetSaveItems(IReadOnlyList<IDataItem> items)
        //{
        //    List<IDataItem> saveItems = new List<IDataItem>();
        //    foreach (IDataItem dataItem in items)
        //    {
        //        if (dataItem is DataNode dataNode)
        //        {
        //            if (dataNode.IsModified || dataNode.HasModifiedChild || dataNode.HasParentModifiedChild)
        //            {
        //                saveItems.Add(dataNode);
        //            }
        //        }

        //        if (dataItem is DataLine dataLine)
        //        {
        //            if (dataLine.Points.Count > 2)
        //            {
        //                saveItems.Add(dataLine);
        //            }
        //        }
        //    }

        //    return saveItems;
        //}


        private static List<IDataItem> GetCacheItems(IReadOnlyList<IDataItem> items) =>
            items
                .Where(item => item is DataNode || item is DataLine)
                .Concat(items.Where(item => item is DataLink)).ToList();


        private static IEnumerable<IDataItem> ToDataItems(Node node)
        {
            yield return ToDataNode(node);

            foreach (var line in node.SourceLines
                .OrderBy(l => $"{l.Source.Name.FullName}->{l.Target.Name.FullName}"))
            {
                yield return ToDataLine(line);
            }

            foreach (var link in node.SourceLinks
                .OrderBy(l => $"{l.Source.Name.FullName}->{l.Target.Name.FullName}"))
            {
                yield return ToDataLink(link);
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


        private static DataNode ToDataNode(Node node) =>
            new DataNode(
                node.Name,
                node.Parent.Name,
                node.NodeType)
            {
                Bounds = node.ViewModel?.ItemBounds ?? node.Bounds,
                Scale = node.ViewModel?.ItemsViewModel?.ItemsCanvas?.ScaleFactor ?? node.ScaleFactor,
                Color = node.ViewModel?.Color ?? node.Color,
                Description = node.Description,
                IsModified = node.IsModified,
                HasParentModifiedChild = node.Parent.HasModifiedChild,
                HasModifiedChild = node.HasModifiedChild,
                ShowState = node.IsHidden ? Node.Hidden : null
            };


        private static DataLine ToDataLine(Line line) =>
            new DataLine(
                line.Source.Name,
                line.Target.Name,
                line.View.MiddlePoints().ToList(),
                line.LinkCount);


        private static DataLink ToDataLink(Link link) =>
            new DataLink(
                link.Source.Name,
                link.Target.Name);


        private string GetCacheFilePath()
        {
            string dataJson = $"{Path.GetFileName(metadata.ModelFilePath)}.dn.json";
            string dataFilePath = Path.Combine(metadata.FolderPath, dataJson);
            return dataFilePath;
        }


        private string GetSaveFilePath()
        {
            string dataJson = $"{Path.GetFileName(metadata.ModelFilePath)}.dpnr";
            string folderPath = Path.GetDirectoryName(metadata.ModelFilePath);
            //  string dataFilePath = Path.Combine(folderPath, dataJson);
            string dataFilePath = Path.Combine(metadata.FolderPath, dataJson);
            return dataFilePath;
        }
    }
}
