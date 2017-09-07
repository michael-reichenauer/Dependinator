using System;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private;
using Dependinator.Utils;


namespace Dependinator.Common.WorkFolders
{
	internal class OpenService : IOpenService
	{
		private readonly IModelViewService modelViewService;
		private readonly IWorkingFolderService workingFolderService;


		public OpenService(
			IModelViewService modelViewService,
			IWorkingFolderService workingFolderService)
		{
			this.modelViewService = modelViewService;
			this.workingFolderService = workingFolderService;
		}

		public async Task OpenFileAsync()
		{
			if (!TryOpenFile())
			{
				return;
			}

			//await SetWorkingFolderAsync();

			await modelViewService.LoadAsync();
		}


		public bool TryOpenFile()
		{
			while (true)
			{
				// Create OpenFileDialog 
				Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

				// Set filter for file extension and default file extension 
				dlg.DefaultExt = ".exe";
				dlg.Filter = "Files (*.exe, *.dll)|*.exe;*.dll|.NET libs (*.dll)|*.dll|.NET Programs (*.exe)|*.exe";
				dlg.CheckFileExists = true;
				dlg.Multiselect = false;
				dlg.Title = "Select a .NET .dll or .exe file";
				dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

				bool? result = dlg.ShowDialog();

				// Get the selected file name and display in a TextBox 
				if (result != true)
				{
					Log.Debug("User canceled selecting a file");
					return false;
				}


				if (workingFolderService.TrySetPath(dlg.FileName))
				{
					Log.Debug($"User selected valid file '{dlg.FileName}'");
					return true;
				}
				else
				{
					Log.Debug($"User selected an invalid file: {dlg.FileName}");
				}
			}
		}


		//private async Task SetWorkingFolderAsync()
		//{
		//	await Task.Yield();

		//	if (ipcRemotingService != null)
		//	{
		//		ipcRemotingService.Dispose();
		//	}

		//	ipcRemotingService = new IpcRemotingService();

		//	string id = ProgramInfo.GetWorkingFolderId(workingFolder);
		//	if (ipcRemotingService.TryCreateServer(id))
		//	{
		//		ipcRemotingService.PublishService(mainWindowIpcService);
		//	}
		//	else
		//	{
		//		// Another instance for that working folder is already running, activate that.
		//		ipcRemotingService.CallService<MainWindowIpcService>(id, service => service.Activate(null));
		//		MediaTypeNames.Application.Current.Shutdown(0);
		//		ipcRemotingService.Dispose();
		//		return;
		//	}

		//	jumpListService.Add(workingFolder.FilePath);

		//	Notify(nameof(Title));

		//	isLoaded = true;
		//}
	}
}