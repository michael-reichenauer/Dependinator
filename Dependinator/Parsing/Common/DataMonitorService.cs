﻿// using System.Runtime.Versioning;
// using Microsoft.AspNetCore.Components;

// namespace Dependinator.Parsing.Common;

// [UnsupportedOSPlatform("browser")]
// [Singleton]
// internal class DataMonitorService : IDataMonitorService
// {
//     const NotifyFilters NotifyFilters =
//        System.IO.NotifyFilters.LastWrite
//        | System.IO.NotifyFilters.FileName
//        | System.IO.NotifyFilters.DirectoryName;

//     static readonly TimeSpan DataChangedTime = TimeSpan.FromSeconds(10);
//     static readonly TimeSpan DataChangingTime = TimeSpan.FromSeconds(5);

//     // private readonly DebounceDispatcher changeDebounce = new DebounceDispatcher();
//     readonly FileSystemWatcher folderWatcher = new FileSystemWatcher();

//     string monitoredMainPath;
//     IReadOnlyList<string> monitoredFiles;
//     string monitoredFolder;

//     public DataMonitorService()
//     {
//         folderWatcher.Changed += (s, e) => FileChange(e);
//         folderWatcher.Created += (s, e) => FileChange(e);
//         folderWatcher.Renamed += (s, e) => FileChange(e);
//     }

//     public event EventHandler DataChangedOccurred;

//     public void StartMonitorData(string mainPath, IReadOnlyList<string> dataPaths)
//     {
//         if (IsMonitoring(mainPath))
//         {
//             return;
//         }

//         dispatcher = Dispatcher.CreateDefault();

//         StopMonitorData();

//         monitoredMainPath = mainPath;
//         monitoredFiles = dataPaths;
//         monitoredFolder = Path.GetDirectoryName(mainPath);

//         StartMonitorData();
//     }

//     private void StartMonitorData()
//     {
//         folderWatcher.Path = monitoredFolder;
//         folderWatcher.NotifyFilter = NotifyFilters;
//         folderWatcher.Filter = "*.*";
//         folderWatcher.IncludeSubdirectories = true;

//         folderWatcher.EnableRaisingEvents = true;
//     }

//     public void StopMonitorData()
//     {
//         changeDebounce.Stop();
//         folderWatcher.EnableRaisingEvents = false;
//     }

//     private void FileChange(FileSystemEventArgs e)
//     {
//         string fullPath = e.FullPath;
//         if (string.IsNullOrEmpty(fullPath) || Directory.Exists(fullPath))
//         {
//             return;
//         }

//         if (monitoredFiles.Any(file => file.IsSameIc(fullPath)) && File.Exists(fullPath))
//         {
//             // Data file has changed, postpone event a little
//             ScheduleDataChange(DataChangedTime);
//             return;
//         }

//         // Data building event, postpone event a little
//         if (changeDebounce.IsTriggered)
//         {
//             ScheduleDataChange(DataChangingTime);
//         }
//     }

//     private void ScheduleDataChange(TimeSpan withinTime)
//     {
//         changeDebounce.Debounce(
//             withinTime, TriggerEvent, null, DispatcherPriority.ApplicationIdle, dispatcher);
//     }

//     private bool IsMonitoring(string mainPath) =>
//         monitoredMainPath != null && mainPath.IsSameIc(monitoredMainPath);

//     private void TriggerEvent(object obj)
//     {
//         if (monitoredFiles.All(File.Exists))
//         {
//             Log.Debug($"Monitored file changed");
//             DataChangedOccurred?.Invoke(this, EventArgs.Empty);
//         }
//         else
//         {
//             Log.Debug($"Some monitored files do not exists");
//         }
//     }
// }
