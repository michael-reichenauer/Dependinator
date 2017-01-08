using System.Collections.Generic;
using System.Windows;
using Dependiator.Common.ThemeHandling;
using Dependiator.Modeling.Analyzing;
using Dependiator.Utils;


namespace Dependiator.Modeling
{
	[SingleInstance]
	internal class ModelService : IModelService
	{
		private readonly IAnalyzerService analyzerService;
		private readonly INodeService nodeService;

		public ModelService(
			IAnalyzerService analyzerService,
			INodeService nodeService)
		{
			this.analyzerService = analyzerService;
			this.nodeService = nodeService;
		}


		public void InitModules()
		{
			ElementTree elementTree = analyzerService.Analyze();
			Timing t = new Timing();
			IEnumerable<Node> enumerable = GetNodes(elementTree);

			nodeService.ShowNodes(enumerable);
			t.Log("Created modules");
		}


		public object MoveNode(Point viewPosition, Vector viewOffset, object movingObject)
		{
			return nodeService.MoveNode(viewPosition, viewOffset, movingObject);
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



		//	private IEnumerable<Node> GetNodes(ElementTree elementTree)
		//{
		//	int count = 0;
		//	foreach (Element element in elementTree.Root.Children)
		//	{
		//		int x = count % 10;
		//		int y = count / 10;

		//		Point position = new Point(x * 250 + 30, y * 150 + 50);
		//		Size size = new Size(200, 100);
		//		Rect bounds = new Rect(position, size);
		//		Module module = new Module(nodeService, element, bounds, null);
		//		nodeService.AddRootNode(module);
		//		yield return module;
		//		count++;
		//	}
		// }
	}
}