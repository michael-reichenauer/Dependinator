using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private;


namespace Dependinator.Common.ModelMetadataFolders.Private
{
	internal class OpenModelService : IOpenModelService
	{
		private readonly IModelMetadataService modelMetadataService;
		private readonly IModelViewService modelViewService;
		private readonly IOpenFileDialogService openFileDialogService;


		public OpenModelService(
			IModelViewService modelViewService,
			IModelMetadataService modelMetadataService,
			IOpenFileDialogService openFileDialogService)
		{
			this.modelViewService = modelViewService;
			this.modelMetadataService = modelMetadataService;
			this.openFileDialogService = openFileDialogService;
		}

		public async Task OpenModelAsync()
		{
			if (!openFileDialogService.TryShowOpenFileDialog(out string modelFilePath))
			{
				return;
			}

			modelMetadataService.SetModelFilePath(modelFilePath);

			await modelViewService.LoadAsync();
		}


		public Task OpenModelAsync(string modelFilePath)
		{
			return Task.CompletedTask;
		}


		public IReadOnlyList<string> GetResentModelFilePaths()
		{
			return new List<string>
			{
				"c:\\Work\\Server.dll",
				"c:\\Work\\Dependiator.exe"
			};
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