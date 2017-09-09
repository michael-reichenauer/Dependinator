﻿using System;
using System.Threading.Tasks;
using System.Windows;
using Dependinator.MainWindowViews;
using Dependinator.ModelViewing.Private;
using Dependinator.Utils;


namespace Dependinator.Common.ModelMetadataFolders.Private
{
	internal class OpenModelService : IOpenModelService
	{
		private readonly IModelMetadataService modelMetadataService;
		private readonly IModelViewService modelViewService;
		private readonly IOpenFileDialogService openFileDialogService;
		private readonly IExistingInstanceService existingInstanceService;
		private readonly Lazy<MainWindow> mainWindow;


		public OpenModelService(
			IModelViewService modelViewService,
			IModelMetadataService modelMetadataService,
			IOpenFileDialogService openFileDialogService,
			IExistingInstanceService existingInstanceService,
			Lazy<MainWindow> mainWindow)
		{
			this.modelViewService = modelViewService;
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

			await modelViewService.LoadAsync();
		}
	}
}