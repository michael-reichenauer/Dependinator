using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using Dependinator.ModelViewing.DataHandling.Private.Parsing;
using Dependinator.Utils;
using Dependinator.Utils.Dependencies;
using Dependinator.Utils.UI;


namespace Dependinator.ModelViewing.DataHandling
{
	[SingleInstance]
	internal class DataMonitorService : IDataMonitorService
	{
		private readonly IParserService parserService;
		private readonly FileSystemWatcher folderWatcher = new FileSystemWatcher();
		private readonly DebounceDispatcher changeDebounce = new DebounceDispatcher();

		private IReadOnlyList<string> monitoredFiles;
		private IReadOnlyList<string> monitoredWorkFolders;
		private Dispatcher dispatcher;

		private const NotifyFilters NotifyFilters =
			System.IO.NotifyFilters.LastWrite
			| System.IO.NotifyFilters.FileName
			| System.IO.NotifyFilters.DirectoryName;


		public DataMonitorService(
			IParserService parserService)
		{
			this.parserService = parserService;

			folderWatcher.Changed += (s, e) => FileChange(e);
			folderWatcher.Created += (s, e) => FileChange(e);
			folderWatcher.Renamed += (s, e) => FileChange(e);
		}


		public event EventHandler ChangedOccurred;


		public void Start(string filePath)
		{
			dispatcher = Dispatcher.CurrentDispatcher;

			Stop();

			folderWatcher.Path = Path.GetDirectoryName(filePath);
			folderWatcher.NotifyFilter = NotifyFilters;
			folderWatcher.Filter = "*.*";
			folderWatcher.IncludeSubdirectories = true;

			monitoredFiles = parserService.GetDataFilePaths(filePath);
			monitoredWorkFolders = parserService.GetBuildPaths(filePath);

			folderWatcher.EnableRaisingEvents = true;
		}


		public void Stop()
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
					return;
				}
			}
		}


		private void Trigger(object obj)
		{
			if (monitoredFiles.All(File.Exists))
			{
				Log.Debug($"Monitored file changed");
				ChangedOccurred?.Invoke(this, EventArgs.Empty);
			}
			else
			{
				Log.Debug($"Some monitored files do not exists");
			}
		}
	}
}