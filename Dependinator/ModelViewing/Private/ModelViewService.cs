using System.Threading.Tasks;
using Dependinator.Common.SettingsHandling;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private
{
	[SingleInstance]
	internal class ModelViewService : IModelViewService
	{
		private readonly ISettings settings;
		private readonly IModelService modelService;



		public ModelViewService(
			ISettings settings,
			IModelService modelService)
		{
			this.settings = settings;
			this.modelService = modelService;
		}



		public async Task LoadAsync(ItemsCanvas rootCanvas)
		{
			modelService.ClearAll();

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

			modelService.Save();
		}

		

		private void StoreViewSettings()
		{
			settings.Edit<WorkFolderSettings>(s =>
				{
					s.Scale = modelService.Root.ItemsCanvas.Scale;
					s.Offset = modelService.Root.ItemsCanvas.Offset;
				});
		}


		private void RestoreViewSettings()
		{
			WorkFolderSettings folderSettings = settings.Get<WorkFolderSettings>();
			modelService.Root.ItemsCanvas.Scale = folderSettings.Scale;
			modelService.Root.ItemsCanvas.Offset = folderSettings.Offset;
		}
	}
}