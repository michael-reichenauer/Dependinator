using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Dependinator.Api;
using Dependinator.Utils;


namespace Dependinator.Common.ModelMetadataFolders.Private
{
	internal class OpenModelService : IOpenModelService
	{
		private readonly IModelMetadataService modelMetadataService;
		private readonly Lazy<ILoadModelService> loadModelService;
		private readonly IOpenFileDialogService openFileDialogService;
		private readonly IExistingInstanceService existingInstanceService;
		private readonly IStartInstanceService startInstanceService;
		private readonly IApiManagerService apiManagerService;


		public OpenModelService(
			Lazy<ILoadModelService> loadModelService,
			IModelMetadataService modelMetadataService,
			IOpenFileDialogService openFileDialogService,
			IExistingInstanceService existingInstanceService,
			IStartInstanceService startInstanceService,
			IApiManagerService apiManagerService)
		{
			this.loadModelService = loadModelService;
			this.modelMetadataService = modelMetadataService;
			this.openFileDialogService = openFileDialogService;
			this.existingInstanceService = existingInstanceService;
			this.startInstanceService = startInstanceService;
			this.apiManagerService = apiManagerService;
		}



		public void ShowOpenModelDialog() => startInstanceService.OpenOrStartDefaultInstance();


		public async Task OpenOtherModelAsync()
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


		private async Task OpenOtherModelAsync(string modelFilePath)
		{
			string metadataFolderPath = modelMetadataService.GetMetadataFolderPath(modelFilePath);

			if (!existingInstanceService.TryActivateExistingInstance(null))
			{
				startInstanceService.StartInstance(modelFilePath);
			}

			if (modelMetadataService.IsDefault)
			{
				// The open model dialog can be closed after opening other model
				await Task.Delay(500);
				Application.Current.Shutdown(0);
			}
		}


		public async Task OpenCurrentModelAsync()
		{
			string metadataFolderPath = modelMetadataService.MetadataFolderPath;

			if (existingInstanceService.TryActivateExistingInstance(null))
			{
				// Another instance for this working folder is already running and it received the
				// command line from this instance, lets exit this instance, while other instance continuous
				Application.Current.Shutdown(0);
				return;
			}

			apiManagerService.Register();

			await loadModelService.Value.LoadAsync();
		}


		public async Task OpenModelAsync(IReadOnlyList<string> modelFilePaths)
		{
			// Currently only support one dropped file
			string modelFilePath = modelFilePaths.First();

			await TryModelAsync(modelFilePath);
		}
	}
}