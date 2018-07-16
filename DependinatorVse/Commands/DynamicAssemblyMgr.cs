using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DependinatorVse.Commands.Private;


namespace DependinatorVse.Commands
{
	public class DynamicAssemblyMgr
	{
		private readonly Func<string> assemblyPath;
		private readonly Func<string> monitorPath;


		private readonly FileSystemWatcher folderWatcher = new FileSystemWatcher();

		private IDynamicAssembly currentInstance;
		private DateTime currentFileTime;

		public event EventHandler LoadedOccurred;


		public DynamicAssemblyMgr(Func<string> assemblyPath, Func<string> monitorPath)
		{
			this.assemblyPath = assemblyPath;
			this.monitorPath = monitorPath;
		}


		public T Resolve<T>(params object[] args)
		{
			try
			{
				return (T)currentInstance.Resolve(typeof(T), args);
			}
			catch (Exception e)
			{
				Log.Error($"Failed to resolve {typeof(T).FullName}, {e}");
				throw;
			}
		}


		public void Load()
		{
			try
			{
				Load(null);

				MonitorFileChanges();
			}
			catch (Exception e)
			{
				Log.Error($"Failed to Load, {e}");
				throw;
			}
		}


		private void Load(object state)
		{
			StoreFileTime();
			currentInstance = LoadDynamicAssembly();

			currentInstance.Load();

			LoadedOccurred?.Invoke(this, EventArgs.Empty);
		}


		private object Unload()
		{
			currentInstance?.UnLoad();
			currentInstance = null;
			return null;
		}


		private async Task ReloadAsync()
		{
			object state = Unload();

			await Task.Delay(500);

			Load(state);
		}


		private IDynamicAssembly LoadDynamicAssembly()
		{
			Log.Debug($"Loading {assemblyPath()}");

			byte[] fileBytes = File.ReadAllBytes(assemblyPath());
			Assembly assembly = Assembly.Load(fileBytes);
			Log.Debug($"Loaded {assembly.FullName}");

			Type[] types;

			try
			{
				types = assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException e)
			{
				Log.Warn($"Failed to load all types, {e}");
				if (e.Types == null)
				{
					Log.Warn($"No types found at all (types == null");
				}

				types = e.Types ?? new Type[0];
			}

			Log.Debug($"Types count: {types.Count(t => t != null)}");

			Type type = types
				.Where(t => t != null)
				.FirstOrDefault(t => t.IsClass && t.GetInterfaces().Contains(typeof(IDynamicAssembly)));

			if (type == null)
			{
				throw new InvalidOperationException("Failed to locate IDynamicAssembly type");
			}

			return (IDynamicAssembly)Activator.CreateInstance(type);
		}




		private void MonitorFileChanges()
		{
			Log.Debug($"Monitor {monitorPath()}");

			folderWatcher.Path = Path.GetDirectoryName(monitorPath());
			folderWatcher.NotifyFilter =
				NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
			folderWatcher.Filter = "*.*";
			folderWatcher.IncludeSubdirectories = false;
			folderWatcher.Changed += (s, e) => FileChanged(e);
			folderWatcher.Created += (s, e) => FileChanged(e);
			folderWatcher.Renamed += (s, e) => FileChanged(e);

			folderWatcher.EnableRaisingEvents = true;
		}


#pragma warning disable VSTHRD100 // Avoid async void methods
		private async void FileChanged(FileSystemEventArgs e)
#pragma warning restore VSTHRD100
		{
			try
			{
				if (0 != string.Compare(e.FullPath, monitorPath(), StringComparison.OrdinalIgnoreCase)
						|| !File.Exists(monitorPath())
						|| IsSameFileTime())
				{
					// Either not the monitored file, assembly file does not exist or has not changed
					return;
				}

				// Try prevent multiple events within a short period
				StoreFileTime();
				await Task.Delay(500);

				await ReloadAsync();
			}
			catch (Exception exception)
			{
				Log.Error($"Error {exception}");
			}
		}


		private bool IsSameFileTime()
		{
			lock (folderWatcher) return currentFileTime == File.GetLastWriteTime(monitorPath());
		}


		private void StoreFileTime()
		{
			lock (folderWatcher) { currentFileTime = File.GetLastWriteTime(monitorPath()); }
		}
	}
}