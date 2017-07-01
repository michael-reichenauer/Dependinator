using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Dependinator.ApplicationHandling;
using Dependinator.ApplicationHandling.SettingsHandling;
using Dependinator.Modeling;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;

namespace Dependinator.ModelViewing
{
	[SingleInstance]
	internal class ModelViewService : IModelViewService
	{
		private readonly WorkingFolder workingFolder;
		private readonly IModelService modelService;



		private Model currentModel;

		public ModelViewService(
			WorkingFolder workingFolder,
			IModelService modelService)
		{
			this.workingFolder = workingFolder;
			this.modelService = modelService;
		}




		public void InitModules(ItemsCanvas rootCanvas)
		{
			Timing t = new Timing();

			currentModel = GetDataModel();

			t.Log($"Get data model {currentModel}");

			//model = modelService.ToModel(dataModel, null);

			t.Log("To model");

			ShowModel(rootCanvas);
			t.Log("Show model");

			t.Log("Showed model");
		}


		private Model GetDataModel()
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


			Model dataModel = GetCachedOrFreshModelData();

			return dataModel;
		}


		public async Task Refresh(ItemsCanvas rootCanvas, bool refreshLayout)
		{
			await Task.Yield();

			Timing t = new Timing();

			StoreViewSettings();
			t.Log("stored setting");

			ModelViewData modelViewData = refreshLayout ? null : modelService.ToViewData(currentModel);
			t.Log("Got current model data");

			currentModel.Root.Clear();

			currentModel = await RefreshElementTreeAsync(modelViewData);

			t.Log("Read fresh data");

			ShowModel(rootCanvas);

			t.Log("Show model");

			t.Log("Refreshed model");
		}


		private Model GetCachedOrFreshModelData()
		{
			Model dataModel;
			if (!TryReadCachedData(out dataModel))
			{
				dataModel = ReadFreshData();
			}

			return dataModel;
		}


		private void ShowModel(ItemsCanvas rootCanvas)
		{
			RestoreViewSettings(rootCanvas);

			Node rootNode = currentModel.Root;

			rootNode.Show(rootCanvas);
		}


		public void Zoom(double zoomFactor, Point zoomCenter) =>
			currentModel.Root.Zoom(zoomFactor, zoomCenter);


		public void Move(Vector viewOffset)
		{
			currentModel.Root.MoveItems(viewOffset);
		}


		private async Task<Model> RefreshElementTreeAsync(ModelViewData modelViewData)
		{
			Model model = await Task.Run(
				() => modelService.Analyze(workingFolder.FilePath, modelViewData));

			return model;
		}


		private bool TryReadCachedData(out Model dataModel)
		{
			string dataFilePath = GetDataFilePath();
			return modelService.TryDeserialize(dataFilePath, out dataModel);
		}


		private Model ReadFreshData()
		{
			Timing t = Timing.Start();
			Model newModel = modelService.Analyze(workingFolder.FilePath, null);
			t.Log("Read fresh model");
			return newModel;
		}


		public void Close()
		{
			currentModel.Root.UpdateAllNodesScalesBeforeClose();
			//DataModel dataModel = modelService.ToDataModel(model);
			string dataFilePath = GetDataFilePath();

			modelService.Serialize(currentModel, dataFilePath);

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
					settings.Scale = currentModel.Root.ItemsScale;
					settings.X = currentModel.Root.ItemsOffset.X;
					settings.Y = currentModel.Root.ItemsOffset.Y;
				});
		}


		private void RestoreViewSettings(ItemsCanvas rootCanvas)
		{
			WorkFolderSettings settings = Settings.GetWorkFolderSetting(workingFolder);
			rootCanvas.Scale = settings.Scale;
			rootCanvas.Offset = new Point(settings.X, settings.Y);
		}


		//	internal class DataModel
		//	{
		//		public List<Data.Node> Nodes { get; set; } = new List<Data.Node>();

		//		public List<Data.Link> Links { get; set; } = new List<Data.Link>();


		//		public DataModel AddType(string name)
		//		{
		//			Nodes.Add(new Data.Node { Name = name, Type = "Type" });
		//			return this;
		//		}

		//		public DataModel AddMember(string name)
		//		{
		//			Nodes.Add(new Data.Node {Name = name, Type = "Member"});
		//			return this;
		//		}

		//		public DataModel AddLink(string source, string target)
		//		{
		//			Links.Add(new Data.Link { Source = source, Target = target });
		//			return this;
		//		}


		//		public override string ToString() => $"{Nodes.Count} nodes, {Links.Count} links.";
		//	}
	}
}