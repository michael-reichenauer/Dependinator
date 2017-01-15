using System.Collections.Generic;
using System.Windows;
using Dependiator.Modeling.Analyzing;
using Dependiator.Modeling.Serializing;
using Dependiator.Utils;


namespace Dependiator.Modeling
{
	[SingleInstance]
	internal class ModelService : IModelService
	{
		private readonly IReflectionService reflectionService;
		private readonly IElementService elementService;
		private readonly INodeService nodeService;
		private readonly IDataSerializer dataSerializer;

		private ElementTree elementTree;

		public ModelService(
			IReflectionService reflectionService,
			IElementService elementService,
			INodeService nodeService,
			IDataSerializer dataSerializer)
		{
			this.reflectionService = reflectionService;
			this.elementService = elementService;
			this.nodeService = nodeService;
			this.dataSerializer = dataSerializer;
		}


		public void InitModules()
		{
			if (!dataSerializer.TryDeserialize(out Data data))
			{
				data = reflectionService.Analyze();
			}

			elementTree = elementService.ToElementTree(data);

			//Data data2 = elementService.ToData(elementTree);
			//serializer.Serialize(data2);

			//elementTree = elementService.ToElementTree(data2);

			//Data data3 = elementService.ToData(elementTree);
			//serializer.Serialize(data3);

			// elementTree = elementService.ToElementTree(data3);

			Timing t = new Timing();
			IEnumerable<Node> enumerable = GetNodes(elementTree);

			nodeService.ShowNodes(enumerable);
			t.Log("Created modules");
		}


		public object MoveNode(Point viewPosition, Vector viewOffset, object movingObject)
		{
			return nodeService.MoveNode(viewPosition, viewOffset, movingObject);
		}


		public void Close()
		{
			Data data = elementService.ToData(elementTree);
			dataSerializer.Serialize(data);
		}


		private IEnumerable<Node> GetNodes(ElementTree elementTree)
		{
			int x = 0;
			int y = 0;

			Point position = new Point(x * 250 + 30, y * 150 + 50);
			Size size = new Size(200, 100);
			Rect bounds = new Rect(position, size);
			Module module = new Module(nodeService, elementTree.Root, bounds, null);
			nodeService.AddRootNode(module);
			yield return module;
		}
	}
}