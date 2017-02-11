using System.Collections.Generic;
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
		private readonly IElementService elementService;
		private readonly IItemService itemService;
		private readonly IDataSerializer dataSerializer;
		private readonly ICanvasService canvasService;

		private ElementTree elementTree;

		public ModelService(
			WorkingFolder workingFolder,
			IReflectionService reflectionService,
			IElementService elementService,
			IItemService itemService,
			IDataSerializer dataSerializer,
			ICanvasService canvasService)
		{
			this.workingFolder = workingFolder;
			this.reflectionService = reflectionService;
			this.elementService = elementService;
			this.itemService = itemService;
			this.dataSerializer = dataSerializer;
			this.canvasService = canvasService;
		}


		public void InitModules()
		{
			Timing t = new Timing();

			DataModel data = GetCachedOrFreshModelData();

			t.Log("After read data");

			ElementTree model = elementService.ToElementTree(data, null);

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

			DataModel currentModelData = elementService.ToData(elementTree);
			t.Log("Got current model data");

			ElementTree tree = await RefreshElementTreeAsync(currentModelData);

			t.Log("Read fresh data");

			ShowModel(tree);

			t.Log("Show model");

			RestoreViewSettings();

			t.Log("Refreshed model");
		}


		private DataModel GetCachedOrFreshModelData()
		{
			DataModel data;
			if (!TryReadCachedData(out data))
			{
				data = ReadFreshData();
			}
			return data;
		}


		private void ShowModel(ElementTree tree)
		{
			if (elementTree != null)
			{
				itemService.ClearAll();
			}

			Item rootItem = GetNode(tree);
			itemService.ShowRootItem(rootItem);
			elementTree = tree;
		}


		private async Task<ElementTree> RefreshElementTreeAsync(DataModel oldData)
		{
			ElementTree tree = await Task.Run(() =>
			{
				DataModel newData = reflectionService.Analyze(workingFolder.FilePath);

				return elementService.ToElementTree(newData, oldData);
			});
			return tree;
		}


		private bool TryReadCachedData(out DataModel data)
		{
			return dataSerializer.TryDeserialize(out data);
		}


		private DataModel ReadFreshData()
		{
			return reflectionService.Analyze(workingFolder.FilePath);
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
			DataModel data = elementService.ToData(elementTree);
			dataSerializer.Serialize(data);

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


		private Item GetNode(ElementTree elementTree)
		{
			Size size = new Size(200000, 100000);
			
			double scale = 1 ;
			itemService.CanvasScale = scale;

			double x = 0 - (size.Width / 2);
			double y = 0 - (size.Height / 2);

			Point position = new Point(x, y);
			Rect bounds = new Rect(position, size);
			Node node = new Node(itemService, elementTree.Root, bounds, null);
			itemService.AddRootItem(node);
			return node;
		}
	}
}