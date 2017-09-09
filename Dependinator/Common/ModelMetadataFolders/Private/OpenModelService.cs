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
		private readonly IRecentModelsService recentModelsService;


		public OpenModelService(
			IModelViewService modelViewService,
			IModelMetadataService modelMetadataService,
			IOpenFileDialogService openFileDialogService,
			IRecentModelsService recentModelsService)
		{
			this.modelViewService = modelViewService;
			this.modelMetadataService = modelMetadataService;
			this.openFileDialogService = openFileDialogService;
			this.recentModelsService = recentModelsService;
		}


		public async Task OpenModelAsync()
		{
			if (!openFileDialogService.TryShowOpenFileDialog(out string modelFilePath))
			{
				return;
			}

			await OpenModelAsync(modelFilePath);
		}


		public async Task OpenModelAsync(string modelFilePath)
		{
			modelMetadataService.SetModelFilePath(modelFilePath);

			await modelViewService.LoadAsync();

			recentModelsService.AddModelPaths(modelFilePath);
		}


		public IReadOnlyList<string> GetResentModelFilePaths() => 
			recentModelsService.GetModelPaths();


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