using System.Threading.Tasks;
using System.Windows;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private
{
	[SingleInstance]
	internal class ModelViewService : IModelViewService
	{
		private readonly IModelService modelService;



		//private ModelOld currentModel;

		public ModelViewService(
			IModelService modelService)
		{
			this.modelService = modelService;

		}



		public async Task LoadAsync(IItemsCanvas rootCanvas)
		{
			modelService.Init(rootCanvas);

			await modelService.RefreshAsync(false);
			await Task.Yield();
		}


		//private ModelOld GetDataModel()
		//{
		//	ModelOld dataModel = GetCachedOrFreshModelData();

		//	return dataModel;
		//}


		public async Task Refresh(IItemsCanvas rootCanvas, bool refreshLayout)
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


		public void Zoom(double zoomFactor, Point zoomCenter) =>
			modelService.ZoomRoot(zoomFactor, zoomCenter);


		public void Move(Vector viewOffset) => modelService.MoveRootItems(viewOffset);


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
			//currentModel.Root.UpdateAllNodesScalesBeforeClose();
			////DataModel dataModel = modelingService.ToDataModel(model);
			//string dataFilePath = GetDataFilePath();

			//modelingService.Serialize(currentModel, dataFilePath);

			//StoreViewSettings();
		}


		//private string GetDataFilePath()
		//{
		//	return Path.Combine(workingFolder, "data.json");
		//}


		//private void StoreViewSettings()
		//{
		//	Settings.EditWorkingFolderSettings(workingFolder,
		//		settings =>
		//		{
		//			settings.Scale = currentModel.Root.ItemsScale;
		//			settings.X = currentModel.Root.ItemsOffset.X;
		//			settings.Y = currentModel.Root.ItemsOffset.Y;
		//		});
		//}


		//private void RestoreViewSettings(IItemsCanvas rootCanvas)
		//{
		//	WorkFolderSettings settings = Settings.GetWorkFolderSetting(workingFolder);
		//	rootCanvas.Scale = settings.Scale;
		//	rootCanvas.Offset = new Point(settings.X, settings.Y);
		//}


	}
}