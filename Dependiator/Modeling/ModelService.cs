using System.Threading.Tasks;
using System.Windows;
using Dependiator.ApplicationHandling;
using Dependiator.ApplicationHandling.SettingsHandling;
using Dependiator.Modeling.Analyzing;
using Dependiator.Modeling.Items;
using Dependiator.Modeling.Serializing;
using Dependiator.Utils;


namespace Dependiator.Modeling
{
	[SingleInstance]
	internal class ModelService : IModelService
	{
		private readonly WorkingFolder workingFolder;
		private readonly IReflectionService reflectionService;
		private readonly INodeService nodeService;
		private readonly INodeItemService nodeItemService;

		private readonly IDataSerializer dataSerializer;


		private Model model;

		public ModelService(
			WorkingFolder workingFolder,
			IReflectionService reflectionService,
			INodeService nodeService,
			INodeItemService nodeItemService,
			IDataSerializer dataSerializer)
		{
			this.workingFolder = workingFolder;
			this.reflectionService = reflectionService;
			this.nodeService = nodeService;
			this.nodeItemService = nodeItemService;
			this.dataSerializer = dataSerializer;
		}




		public void InitModules(ItemsCanvas rootCanvas)
		{
			Timing t = new Timing();

			DataModel dataModel = GetDataModel();

			t.Log("After read data");

			model = nodeService.ToModel(dataModel, null);

			t.Log("To model");

			ShowModel(rootCanvas);
			t.Log("Show model");

			RestoreViewSettings();

			t.Log("Showed model");
		}


		private DataModel GetDataModel()
		{
			DataModel dataModel = new DataModel()
					.AddType("Axis.Ns1")
					.AddType("Axis.Ns2")
					.AddType("Axis.Ns1.Class1")
					.AddType("Axis.Ns1.Class2")
					.AddType("Axis.Ns2.Class1")
					.AddType("Axis.Ns2.NS3.Class1")
					.AddType("Other.Ns1.Class1")
					.AddType("Other.Ns2")
					.AddType("Other.Ns3")
					.AddType("Other.Ns4")
					.AddType("Other.Ns5")
					.AddType("Other.Ns6")
					.AddLink("Axis.Ns1.Class1", "Axis.Ns1.Class2")
					.AddLink("Axis.Ns1.Class1", "Axis.Ns2.Class2")
				;


			dataModel = GetCachedOrFreshModelData();

			return dataModel;
		}


		public async Task Refresh(ItemsCanvas rootCanvas)
		{
			await Task.Yield();

			Timing t = new Timing();

			StoreViewSettings();
			t.Log("stored setting");

			ModelViewData modelViewData = nodeService.ToViewData(model);
			t.Log("Got current model data");

			model = await RefreshElementTreeAsync(modelViewData);

			t.Log("Read fresh data");

			ShowModel(rootCanvas);

			t.Log("Show model");

			RestoreViewSettings();

			t.Log("Refreshed model");
		}


		private DataModel GetCachedOrFreshModelData()
		{
			DataModel dataModel;
			//if (!TryReadCachedData(out dataModel))
			{
				dataModel = ReadFreshData();
			}

			return dataModel;
		}


		private void ShowModel(ItemsCanvas rootCanvas)
		{
			//if (elementTree != null)
			//{
			//	itemService.ClearAll();
			//}

			//Node rootNode = GetNode(model);


			//nodesViewModel.AddItem(rootNode);

			Node rootNode = model.Root;
	
			rootCanvas.Scale = 1.0;
			rootNode.Show(rootCanvas);

			//nodeItemService.SetChildrenItemBounds(rootNode);

			//nodesViewModel.AddItems(rootNode.ChildNodes);

			//rootNode.Zoom(nodesViewModel.Scale);

			//itemService.ShowRootItem(rootItem);

		}


		public void Zoom(double zoomFactor, Point zoomCenter) =>
			model.Root.Zoom(zoomFactor, zoomCenter);


		public void Move(Vector viewOffset)
		{
			model.Root.MoveChildren(viewOffset);
		}


		private async Task<Model> RefreshElementTreeAsync(ModelViewData modelViewData)
		{
			Model model = await Task.Run(() =>
			{
				DataModel dataModel = reflectionService.Analyze(workingFolder.FilePath);

				return nodeService.ToModel(dataModel, modelViewData);
			});

			return model;
		}


		private bool TryReadCachedData(out DataModel dataModel)
		{
			return dataSerializer.TryDeserialize(out dataModel);
		}


		private DataModel ReadFreshData()
		{
			DataModel model = reflectionService.Analyze(workingFolder.FilePath);

			return model;
		}


		public object MoveNode(Point viewPosition, Vector viewOffset, object movingObject)
		{
			return null;
			//return itemService.MoveItem(viewPosition, viewOffset, movingObject);
		}


		public bool ZoomNode(int zoomDelta, Point viewPosition)
		{
			return false;
			//return itemService.ZoomItem(zoomDelta, viewPosition);
		}


		public void Close()
		{
			DataModel dataModel = nodeService.ToDataModel(model);
			dataSerializer.Serialize(dataModel);

			StoreViewSettings();
		}


		private void StoreViewSettings()
		{
			Settings.EditWorkingFolderSettings(workingFolder,
				settings =>
				{
					//settings.Scale = canvasService.Scale;
					//settings.X = canvasService.Offset.X;
					//settings.Y = canvasService.Offset.Y;
				});
		}


		private void RestoreViewSettings()
		{
			WorkFolderSettings settings = Settings.GetWorkFolderSetting(workingFolder);

			//canvasService.Scale = settings.Scale;
			//canvasService.Offset = new Point(settings.X, settings.Y);
		}


		//private Node GetNode(Model model)
		//{
		//	Size size = new Size(200, 100);

		//	//double scale = 1;
		//	//itemService.CanvasScale = scale;

		//	double x = 100;
		//	double y = 100;

		//	Point position = new Point(x, y);
		//	//Rect bounds = new Rect(position, size);
		//	Node node = model.Root;
		//	//node.SetInitalRootNodeBounds(bounds);
		//	return node;
		//}
	}
}