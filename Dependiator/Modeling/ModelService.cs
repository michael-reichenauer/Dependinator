﻿using System.Collections.Generic;
using System.Windows;
using Dependiator.Common.ThemeHandling;
using Dependiator.Utils;


namespace Dependiator.Modeling
{
	[SingleInstance]
	internal class ModelService : IModelService
	{
		private readonly INodeService nodeService;
		private readonly IThemeService themeService;

		public ModelService(
			INodeService nodeService,
			IThemeService themeService)
		{
			this.nodeService = nodeService;
			this.themeService = themeService;
		}


		public void InitModules()
		{
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

					//foreach (var node in module.GetShowableNodes())
					//{
					//	yield return node;
					//}
				}
			}
		}
	}
}