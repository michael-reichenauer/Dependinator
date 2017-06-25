using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Dependinator.ApplicationHandling;
using Dependinator.ApplicationHandling.SettingsHandling;
using Dependinator.ModelViewing;
using Dependinator.ModelViewing.Analyzing;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Serializing;
using Dependinator.Utils;


namespace Dependinator.MainViews.Private
{
	[SingleInstance]
	internal class ModelViewService : IModelViewService
	{
		private readonly WorkingFolder workingFolder;
		private readonly IReflectionService reflectionService;
		private readonly IModelService modelService;
		

		private readonly IDataSerializer dataSerializer;


		private Model model;

		public ModelViewService(
			WorkingFolder workingFolder,
			IReflectionService reflectionService,
			IModelService modelService,
			IDataSerializer dataSerializer)
		{
			this.workingFolder = workingFolder;
			this.reflectionService = reflectionService;
			this.modelService = modelService;
			this.dataSerializer = dataSerializer;
		}




		public void InitModules(ItemsCanvas rootCanvas)
		{
			Timing t = new Timing();

			DataModel dataModel = GetDataModel();

			t.Log($"Get data model {dataModel}");

			model = modelService.ToModel(dataModel, null);

			t.Log("To model");

			ShowModel(rootCanvas);
			t.Log("Show model");

			t.Log("Showed model");
		}


		private DataModel GetDataModel()
		{
			//DataModel dataModel = new DataModel()
			//		.AddType("Axis.Ns1")	
			//		.AddType("Other.Ns2")			
			//		.AddLink("Axis.Ns1", "Other.Ns2")

			//	;

			//DataModel dataModel = new DataModel()
			//		.AddType("Axis.Ns1")
			//		.AddType("Axis.Ns2")
			//		.AddType("Axis.Ns1.Class1")
			//		.AddType("Axis.Ns1.Class2")
			//		.AddType("Axis.Ns2.Class1")
			//		.AddType("Axis.Ns2.NS3.Class1")
			//		.AddType("Other.Ns1.Class1")
			//		.AddType("Other.Ns2")
			//		.AddType("Other.Ns3")
			//		.AddType("Other.Ns4")
			//		.AddType("Other.Ns5")
			//		.AddType("Other.Ns6")
			//		.AddLink("Axis.Ns1.Class1", "Axis.Ns1.Class2")
			//		.AddLink("Axis.Ns1.Class1", "Axis.Ns2.Class2")
			//		.AddLink("Axis.Ns1.Class1", "Other.Ns1.Class1")
			//	;


			DataModel dataModel = GetCachedOrFreshModelData();

			return dataModel;
		}


		public async Task Refresh(ItemsCanvas rootCanvas, bool refreshLayout)
		{
			await Task.Yield();

			Timing t = new Timing();

			StoreViewSettings();
			t.Log("stored setting");

			ModelViewData modelViewData = refreshLayout ? null : modelService.ToViewData(model);
			t.Log("Got current model data");

			model.Root.Clear();

			model = await RefreshElementTreeAsync(modelViewData);

			t.Log("Read fresh data");

			ShowModel(rootCanvas);

			t.Log("Show model");

			t.Log("Refreshed model");
		}


		private DataModel GetCachedOrFreshModelData()
		{
			DataModel dataModel;
			if (!TryReadCachedData(out dataModel))
			{
				dataModel = ReadFreshData();
			}

			return dataModel;
		}


		private void ShowModel(ItemsCanvas rootCanvas)
		{
			RestoreViewSettings(rootCanvas);

			Node rootNode = model.Root;

			rootNode.Show(rootCanvas);
		}


		public void Zoom(double zoomFactor, Point zoomCenter) =>
			model.Root.Zoom(zoomFactor, zoomCenter);


		public void Move(Vector viewOffset)
		{
			model.Root.MoveItems(viewOffset);
		}


		private async Task<Model> RefreshElementTreeAsync(ModelViewData modelViewData)
		{
			Model model = await Task.Run(() =>
			{
				DataModel dataModel = reflectionService.Analyze(workingFolder.FilePath);

				return modelService.ToModel(dataModel, modelViewData);
			});

			return model;
		}


		private bool TryReadCachedData(out DataModel dataModel)
		{
			string dataFilePath = GetDataFilePath();
			return dataSerializer.TryDeserialize(dataFilePath, out dataModel);
		}


		private DataModel ReadFreshData()
		{
			Timing t = Timing.Start();
			DataModel newModel = reflectionService.Analyze(workingFolder.FilePath);
			t.Log("Read fresh model");
			return newModel;
		}


		public void Close()
		{
			model.Root.UpdateAllNodesScalesBeforeClose();
			DataModel dataModel = modelService.ToDataModel(model);
			string dataFilePath = GetDataFilePath();

			dataSerializer.Serialize(dataModel, dataFilePath);

			StoreViewSettings();
		}


		private string GetDataFilePath()
		{
			return Path.Combine(workingFolder, "data.json");
		}


		private void StoreViewSettings()
		{
			Settings.EditWorkingFolderSettings(workingFolder,
				settings =>
				{
					settings.Scale = model.Root.ItemsScale;
					settings.X = model.Root.ItemsOffset.X;
					settings.Y = model.Root.ItemsOffset.Y;
				});
		}


		private void RestoreViewSettings(ItemsCanvas rootCanvas)
		{
			WorkFolderSettings settings = Settings.GetWorkFolderSetting(workingFolder);
			rootCanvas.Scale = settings.Scale;
			rootCanvas.Offset = new Point(settings.X, settings.Y);
		}
	}
}