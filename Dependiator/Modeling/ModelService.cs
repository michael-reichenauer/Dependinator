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
			Data data;
			//if (!dataSerializer.TryDeserialize(out data))
			{
				data = reflectionService.Analyze(workingFolder.FilePath);
			}
			t.Log("After read data");

			if (elementTree != null)
			{
				itemService.ClearAll();
			}

			elementTree = elementService.ToElementTree(data, null);
		
			
			Item rootItem = GetNode(elementTree);

			itemService.ShowRootNode(rootItem);

			WorkFolderSettings settings = Settings.GetWorkFolderSetting(workingFolder);

			canvasService.Scale = settings.Scale;
			double x = settings.X;
			double y = settings.Y;
			canvasService.Offset = new Point(x, y);

			t.Log("Created modules");
		}


		public async Task Refresh()
		{
			await Task.Yield();

			Timing t = new Timing();

			Data oldData = elementService.ToData(elementTree);

			ElementTree tree = await Task.Run(() =>
			{
				Data newData = reflectionService.Analyze(workingFolder.FilePath);

				return elementService.ToElementTree(newData, oldData);
			});
		
			t.Log("Read fresh data");

			var scale = canvasService.Scale;
			var offset = canvasService.Offset;

			if (elementTree != null)
			{
				itemService.ClearAll();
			}

			elementTree = tree;
			t.Log("ToElementTree");

			Item rootItem = GetNode(elementTree);

			itemService.ShowRootNode(rootItem);

			canvasService.Scale = scale;
			canvasService.Offset = offset;

			t.Log("Created modules");
		}


		public object MoveNode(Point viewPosition, Vector viewOffset, object movingObject)
		{
			return itemService.MoveNode(viewPosition, viewOffset, movingObject);
		}


		public void Close()
		{
			Data data = elementService.ToData(elementTree);
			dataSerializer.Serialize(data);

			Settings.EditWorkingFolderSettings(workingFolder,
				settings =>
				{
					settings.Scale = canvasService.Scale;
					settings.X = canvasService.Offset.X;
					settings.Y = canvasService.Offset.Y;
				});
		}


		private Item GetNode(ElementTree elementTree)
		{
			Size size = new Size(200000, 100000);
			
			double scale = 1 ;
			itemService.Scale = scale;

			double x = 0 - (size.Width / 2);
			double y = 0 - (size.Height / 2);

			Point position = new Point(x, y);
			Rect bounds = new Rect(position, size);
			Node node = new Node(itemService, elementTree.Root, bounds, null);
			itemService.AddRootNode(node);
			return node;
		}
	}
}