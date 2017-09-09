using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private;
using Dependinator.Utils;


namespace Dependinator.Common.ModelMetadataFolders.Private
{
	internal class OpenModelService : IOpenModelService
	{
		private readonly IModelMetadataService modelMetadataService;
		private readonly IModelViewService modelViewService;
		private readonly IOpenFileDialogService openFileDialogService;
		private readonly IRecentModelsService recentModelsService;
		private readonly IExistingInstanceService existingInstanceService;


		public OpenModelService(
			IModelViewService modelViewService,
			IModelMetadataService modelMetadataService,
			IOpenFileDialogService openFileDialogService,
			IRecentModelsService recentModelsService,
			IExistingInstanceService existingInstanceService)
		{
			this.modelViewService = modelViewService;
			this.modelMetadataService = modelMetadataService;
			this.openFileDialogService = openFileDialogService;
			this.recentModelsService = recentModelsService;
			this.existingInstanceService = existingInstanceService;
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
			if (modelMetadataService.ModelFilePath.IsSameIgnoreCase(modelFilePath))
			{
				Log.Debug("User tries to open same model that is already open");
				return;
			}

			modelMetadataService.SetModelFilePath(modelFilePath);

			if (!existingInstanceService.TryRegisterPath(modelMetadataService.MetadataFolderPath))
			{
				Log.Debug("Other instance is showing this model has been activated.");
				return;
			}
			
			await modelViewService.LoadAsync();

			recentModelsService.AddModelPaths(modelFilePath);
		}


		public IReadOnlyList<string> GetResentModelFilePaths() => recentModelsService.GetModelPaths();
	}
}