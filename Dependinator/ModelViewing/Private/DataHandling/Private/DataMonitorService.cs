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

        private static readonly TimeSpan DataChangedTime = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan DataChangingTime = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan DataNewTime = TimeSpan.FromSeconds(5);

        private readonly IDataFilePaths dataFilePaths;

        private readonly DebounceDispatcher changeDebounce = new DebounceDispatcher();
        private readonly FileSystemWatcher folderWatcher = new FileSystemWatcher();
        private Dispatcher dispatcher;

        private DataFile monitoredDataFile;
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
            if (IsMonitoring(dataFile))
            {
                return;
            }

            dispatcher = Dispatcher.CurrentDispatcher;

            StopMonitorData();

            monitoredDataFile = dataFile;
            monitoredFiles = dataFilePaths.GetDataFilePaths(dataFile);
            monitoredWorkFolders = dataFilePaths.GetBuildPaths(dataFile);

            StartMonitorData();
        }


        public void TriggerDataChanged()
        {
            Log.Debug("Schedule data change event");
            ScheduleDataChange(DataNewTime);
        }


        private void StartMonitorData()
        {
            folderWatcher.Path = GetMonitorRootFolderPath(monitoredFiles, monitoredWorkFolders);
            folderWatcher.NotifyFilter = NotifyFilters;
            folderWatcher.Filter = "*.*";
            folderWatcher.IncludeSubdirectories = true;

            folderWatcher.EnableRaisingEvents = true;
        }


        public void StopMonitorData()
        {
            changeDebounce.Stop();
            folderWatcher.EnableRaisingEvents = false;
        }


        private static string GetMonitorRootFolderPath(
            IEnumerable<string> files,
            IEnumerable<string> workFolders)
        {
            List<string> folders = files.Select(Path.GetDirectoryName).Concat(workFolders).ToList();
            string rootPath = folders.Last();
            bool isRoot;

            do
            {
                isRoot = true;

                foreach (string folder in folders)
                {
                    if (!folder.StartsWithIc(rootPath))
                    {
                        // The current root path was not root to this folder, lets retry with parent
                        isRoot = false;
                        rootPath = Path.GetDirectoryName(rootPath);
                        break;
                    }
                }
            } while (!isRoot);

            return rootPath;
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
                // Data file has changed, postpone event a little 
                ScheduleDataChange(DataChangedTime);
                return;
            }

            if (monitoredWorkFolders.Any(folder => fullPath.StartsWithIc(folder)))
            {
                // Data building event, postpone event a little 
                if (changeDebounce.IsTriggered)
                {
                    ScheduleDataChange(DataChangingTime);
                }
            }
        }


        private void ScheduleDataChange(TimeSpan withinTime)
        {
            changeDebounce.Debounce(
                withinTime, TriggerEvent, null, DispatcherPriority.ApplicationIdle, dispatcher);
        }


        private bool IsMonitoring(DataFile dataFile) => 
            monitoredDataFile != null &&
            dataFile.FilePath.IsSameIgnoreCase(monitoredDataFile.FilePath);


        private void TriggerEvent(object obj)
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
