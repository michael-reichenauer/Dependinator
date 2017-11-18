using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
//using Dependinator.ModelViewing.Private;
using Dependinator.Utils;


namespace Dependinator.Common.ModelMetadataFolders.Private
{
	internal class OpenModelService : IOpenModelService
	{
		private readonly IModelMetadataService modelMetadataService;
		private readonly ILoadModelService loadModelService;
		private readonly IOpenFileDialogService openFileDialogService;
		private readonly IExistingInstanceService existingInstanceService;
		private readonly Lazy<IMainWindow> mainWindow;


		public OpenModelService(
			ILoadModelService loadModelService,
			IModelMetadataService modelMetadataService,
			IOpenFileDialogService openFileDialogService,
			IExistingInstanceService existingInstanceService,
			Lazy<IMainWindow> mainWindow)
		{
			this.loadModelService = loadModelService;
			this.modelMetadataService = modelMetadataService;
			this.openFileDialogService = openFileDialogService;
			this.existingInstanceService = existingInstanceService;
			this.mainWindow = mainWindow;
		}


		public async Task OpenModelAsync()
		{
			if (!openFileDialogService.TryShowOpenFileDialog(out string modelFilePath))
			{
				return;
			}

			await TryModelAsync(modelFilePath);
		}


		public async Task TryModelAsync(string modelFilePath)
		{
			if (modelMetadataService.ModelFilePath.IsSameIgnoreCase(modelFilePath))
			{
				Log.Debug("User tries to open same model that is already open");
				return;
			}

			await OpenOtherModelAsync(modelFilePath);
		}


		public async Task OpenOtherModelAsync(string modelFilePath)
		{
			modelMetadataService.SetModelFilePath(modelFilePath);
			string metadataFolderPath = modelMetadataService.MetadataFolderPath;

			if (existingInstanceService.TryActivateExistingInstance(metadataFolderPath, null))
			{
				// Another instance for this working folder is already running and it received the
				// command line from this instance, lets exit this instance, while other instance continuous
				Application.Current.Shutdown(0);
				return;
			}

			existingInstanceService.RegisterPath(metadataFolderPath);

			mainWindow.Value.RestoreWindowSettings();

			await loadModelService.LoadAsync();
		}




		public async Task OpenModelAsync(IReadOnlyList<string> modelFilePaths)
		{
			// Currently only support one dropped file
			string modelFilePath = modelFilePaths.First();

			await TryModelAsync(modelFilePath);
		}
	}
}