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
			analyzerService.Analyze();
			Timing t = new Timing();
			nodeService.ShowNodes(GetNodes());
			t.Log("Created modules");
		}


		private IEnumerable<Node> GetNodes()
		{
			int total = 20;

			for (int y = 0; y < total; y++)
			{
				for (int x = 0; x < total; x++)
				{
					Module module = new Module(nodeService, $"Name {x},{y}", new Point(x * 200, y * 200), null);
					nodeService.AddRootNode(module);

					yield return module;
				}
			}
		}
	}
}