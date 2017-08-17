﻿using System.Threading.Tasks;
using System.Windows;
using Dependinator.ApplicationHandling;
using Dependinator.ApplicationHandling.SettingsHandling;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private
{
	[SingleInstance]
	internal class ModelViewService : IModelViewService
	{
		private readonly IModelService modelService;
		private readonly WorkingFolder workingFolder;


		public ModelViewService(
			IModelService modelService,
			WorkingFolder workingFolder)
		{
			this.modelService = modelService;
			this.workingFolder = workingFolder;
		}



		public async Task LoadAsync(ItemsCanvas rootCanvas)
		{
			modelService.Init(rootCanvas);
			RestoreViewSettings();

			await modelService.LoadAsync();
		}


		public async Task Refresh(ItemsCanvas rootCanvas, bool refreshLayout)
		{
			await modelService.RefreshAsync(refreshLayout);
		}


		//private ModelOld GetCachedOrFreshModelData()
		//{
		//	ModelOld dataModel;
		//	if (!TryReadCachedData(out dataModel))
		//	{
		//		dataModel = ReadFreshData();
		//	}

		//	return dataModel;
		//}


		//private void ShowModel(IItemsCanvas rootCanvas)
		//{
		//	RestoreViewSettings(rootCanvas);

		//	NodeOld rootNode = currentModel.Root;

		//	rootNode.Show(rootCanvas);
		//}



		//private bool TryReadCachedData(out ModelOld dataModel)
		//{
		//	string dataFilePath = GetDataFilePath();
		//	return modelingService.TryDeserialize(dataFilePath, out dataModel);
		//}


		//private ModelOld ReadFreshData()
		//{
		//	Timing t = Timing.Start();
		//	ModelOld newModel = modelingService.Analyze(workingFolder.FilePath, null);
		//	t.Log("Read fresh model");
		//	return newModel;
		//}


		public void Close()
		{
			StoreViewSettings();

			//currentModel.Root.UpdateAllNodesScalesBeforeClose();
			////DataModel dataModel = modelingService.ToDataModel(model);
			//string dataFilePath = GetDataFilePath();

			//modelingService.Serialize(currentModel, dataFilePath);
		}


		//private string GetDataFilePath()
		//{
		//	return Path.Combine(workingFolder, "data.json");
		//}


		private void StoreViewSettings()
		{
			Settings.EditWorkingFolderSettings(workingFolder,
				settings =>
				{
					settings.Scale = modelService.Root.ItemsCanvas.Scale;
					settings.X = modelService.Root.ItemsCanvas.Offset.X;
					settings.Y = modelService.Root.ItemsCanvas.Offset.Y;
				});
		}


		private void RestoreViewSettings()
		{
			WorkFolderSettings settings = Settings.GetWorkFolderSetting(workingFolder);
			modelService.Root.ItemsCanvas.Scale = settings.Scale;
			modelService.Root.ItemsCanvas.Offset = new Point(settings.X, settings.Y);
		}
	}
}