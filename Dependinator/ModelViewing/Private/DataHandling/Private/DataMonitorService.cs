using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils;
using Dependinator.Utils.Dependencies;
using Dependinator.Utils.UI;


namespace Dependinator.ModelViewing.Private.DataHandling.Private
{
    [SingleInstance]
    internal class DataMonitorService : IDataMonitorService
    {
        private const NotifyFilters NotifyFilters =
            System.IO.NotifyFilters.LastWrite
            | System.IO.NotifyFilters.FileName
            | System.IO.NotifyFilters.DirectoryName;

        private readonly IDataFilePaths dataFilePaths;

        private readonly DebounceDispatcher changeDebounce = new DebounceDispatcher();
        private readonly FileSystemWatcher folderWatcher = new FileSystemWatcher();
        private Dispatcher dispatcher;

        private IReadOnlyList<string> monitoredFiles;
        private IReadOnlyList<string> monitoredWorkFolders;


        public DataMonitorService(IDataFilePaths dataFilePaths)
        {
            this.dataFilePaths = dataFilePaths;

            folderWatcher.Changed += (s, e) => FileChange(e);
            folderWatcher.Created += (s, e) => FileChange(e);
            folderWatcher.Renamed += (s, e) => FileChange(e);
        }


        public event EventHandler DataChangedOccurred;


        public void StartMonitorData(DataFile dataFile)
        {
            dispatcher = Dispatcher.CurrentDispatcher;

            StopMonitorData();

            folderWatcher.Path = dataFilePaths.GetDataFolderPath(dataFile);
            folderWatcher.NotifyFilter = NotifyFilters;
            folderWatcher.Filter = "*.*";
            folderWatcher.IncludeSubdirectories = true;

            monitoredFiles = dataFilePaths.GetDataFilePaths(dataFile);
            monitoredWorkFolders = dataFilePaths.GetBuildPaths(dataFile);

            folderWatcher.EnableRaisingEvents = true;
        }


        public void StopMonitorData()
        {
            changeDebounce.Stop();
            folderWatcher.EnableRaisingEvents = false;
        }


        private void FileChange(FileSystemEventArgs e)
        {
            string fullPath = e.FullPath;
            if (string.IsNullOrEmpty(fullPath) || Directory.Exists(fullPath))
            {
                return;
            }

            if (monitoredFiles.Any(file => file.IsSameIgnoreCase(fullPath)) && File.Exists(fullPath))
            {
                // Log.Debug($"Monitored file {fullPath}, {e.ChangeType}, {changeDebounce.IsTriggered}");
                changeDebounce.Debounce(
                    TimeSpan.FromSeconds(10), Trigger, null, DispatcherPriority.ApplicationIdle, dispatcher);
                return;
            }

            if (monitoredWorkFolders.Any(folder => fullPath.StartsWithIc(folder)))
            {
                if (changeDebounce.IsTriggered)
                {
                    // Log.Debug($"Work item {fullPath}, {e.ChangeType}");
                    changeDebounce.Debounce(
                        TimeSpan.FromSeconds(5), Trigger, null, DispatcherPriority.ApplicationIdle, dispatcher);
                }
            }
        }


        private void Trigger(object obj)
        {
            if (monitoredFiles.All(File.Exists))
            {
                Log.Debug($"Monitored file changed");
                DataChangedOccurred?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Log.Debug($"Some monitored files do not exists");
            }
        }
    }
}
