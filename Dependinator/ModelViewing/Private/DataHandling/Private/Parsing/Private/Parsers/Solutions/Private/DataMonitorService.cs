using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using Dependinator.Utils;
using Dependinator.Utils.Dependencies;
using Dependinator.Utils.UI;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.Parsers.Solutions.Private
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

        private readonly DebounceDispatcher changeDebounce = new DebounceDispatcher();
        private readonly FileSystemWatcher folderWatcher = new FileSystemWatcher();
        private Dispatcher dispatcher;

        private string monitoredDataFile;
        private IReadOnlyList<string> monitoredFiles;
        private string monitoredFolder;


        public DataMonitorService()
        {
            folderWatcher.Changed += (s, e) => FileChange(e);
            folderWatcher.Created += (s, e) => FileChange(e);
            folderWatcher.Renamed += (s, e) => FileChange(e);
        }


        public event EventHandler DataChangedOccurred;


        public void StartMonitorData(string solutionPath, IReadOnlyList<string> dataPaths)
        {
            if (IsMonitoring(solutionPath))
            {
                return;
            }

            dispatcher = Dispatcher.CurrentDispatcher;

            StopMonitorData();

            monitoredDataFile = solutionPath;
            monitoredFiles = dataPaths;
            monitoredFolder = Path.GetDirectoryName(solutionPath);

            StartMonitorData();
        }



        private void StartMonitorData()
        {
            folderWatcher.Path = monitoredFolder;
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


        private void FileChange(FileSystemEventArgs e)
        {
            string fullPath = e.FullPath;
            if (string.IsNullOrEmpty(fullPath) || Directory.Exists(fullPath))
            {
                return;
            }

            if (monitoredFiles.Any(file => file.IsSameIc(fullPath)) && File.Exists(fullPath))
            {
                // Data file has changed, postpone event a little 
                ScheduleDataChange(DataChangedTime);
                return;
            }


            // Data building event, postpone event a little 
            if (changeDebounce.IsTriggered)
            {
                ScheduleDataChange(DataChangingTime);
            }
        }


        private void ScheduleDataChange(TimeSpan withinTime)
        {
            changeDebounce.Debounce(
                withinTime, TriggerEvent, null, DispatcherPriority.ApplicationIdle, dispatcher);
        }


        private bool IsMonitoring(string dataFile) =>
            monitoredDataFile != null && dataFile.IsSameIc(monitoredDataFile);


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
