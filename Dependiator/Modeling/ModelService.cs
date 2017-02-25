using System.Threading.Tasks;
using System.Windows;
using Dependiator.ApplicationHandling;
using Dependiator.ApplicationHandling.SettingsHandling;
using Dependiator.MainViews;
using Dependiator.Modeling.Analyzing;
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
		private readonly IItemService itemService;
		private readonly IDataSerializer dataSerializer;
		private readonly ICanvasService canvasService;

		private Model elementTree;

		public ModelService(
			WorkingFolder workingFolder,
			IReflectionService reflectionService,
			INodeService nodeService,
			IItemService itemService,
			IDataSerializer dataSerializer,
			ICanvasService canvasService)
		{
			this.workingFolder = workingFolder;
			this.reflectionService = reflectionService;
			this.nodeService = nodeService;
			this.itemService = itemService;
			this.dataSerializer = dataSerializer;
			this.canvasService = canvasService;
		}




		public void InitModules()
		{
			Timing t = new Timing();

			DataModel dataModel = new DataModel()
				.AddType("Axis.Ns1")
				.AddType("Axis.Ns2")
				.AddType("Axis.Ns1.Class1")
				.AddType("Axis.Ns1.Class2")
				.AddType("Axis.Ns2.Class1")
				.AddLink("Axis.Ns1.Class1", "Axis.Ns1.Class2")
				.AddLink("Axis.Ns1.Class1", "Axis.Ns2.Class2")
				;


			//DataModel dataModel = GetCachedOrFreshModelData();

			t.Log("After read data");

			Model model = nodeService.ToModel(dataModel, null);

			t.Log("To model");

			ShowModel(model);
			t.Log("Show model");

			RestoreViewSettings();

			t.Log("Showed model");
		}


		public async Task Refresh()
		{
			await Task.Yield();

			Timing t = new Timing();

			StoreViewSettings();
			t.Log("stored setting");

			ModelViewData modelViewData = nodeService.ToViewData(elementTree);
			t.Log("Got current model data");

			Model tree = await RefreshElementTreeAsync(modelViewData);

			t.Log("Read fresh data");

			ShowModel(tree);

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


		private void ShowModel(Model tree)
		{
			if (elementTree != null)
			{
				itemService.ClearAll();
			}

			Item rootItem = GetNode(tree);
			itemService.ShowRootItem(rootItem);
			elementTree = tree;
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
			return itemService.MoveItem(viewPosition, viewOffset, movingObject);
		}


		public bool ZoomNode(int zoomDelta, Point viewPosition)
		{
			return itemService.ZoomItem(zoomDelta, viewPosition);
		}


		public void Close()
		{
			DataModel dataModel = nodeService.ToDataModel(elementTree);
			dataSerializer.Serialize(dataModel);

			StoreViewSettings();
		}


		private void StoreViewSettings()
		{
			Settings.EditWorkingFolderSettings(workingFolder,
				settings =>
				{
					settings.Scale = canvasService.Scale;
					settings.X = canvasService.Offset.X;
					settings.Y = canvasService.Offset.Y;
				});
		}


		private void RestoreViewSettings()
		{
			WorkFolderSettings settings = Settings.GetWorkFolderSetting(workingFolder);

			canvasService.Scale = settings.Scale;
			canvasService.Offset = new Point(settings.X, settings.Y);
		}


		private Item GetNode(Model model)
		{
			Size size = new Size(200000, 100000);

			double scale = 1;
			itemService.CanvasScale = scale;

			double x = 0 - (size.Width / 2);
			double y = 0 - (size.Height / 2);

			Point position = new Point(x, y);
			Rect bounds = new Rect(position, size);
			Node node = model.Root;
			node.SetBounds(bounds);
			itemService.AddRootItem(node);
			return node;
		}
	}
}