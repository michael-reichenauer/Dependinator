﻿using System;
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
        private string monitoredFolder;


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
            monitoredFolder = Path.GetDirectoryName(dataFile.FilePath);

            StartMonitorData();
        }


        public void TriggerDataChanged()
        {
            Log.Debug("Schedule data change event");
            ScheduleDataChange(DataNewTime);
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


        private bool IsMonitoring(DataFile dataFile) =>
            monitoredDataFile != null &&
            dataFile.FilePath.IsSameIc(monitoredDataFile.FilePath);


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
