using System;
using System.Collections.Generic;
using System.Windows;
using Dependiator.Common.ThemeHandling;
using Dependiator.MainViews;
using Dependiator.MainViews.Private;
using Dependiator.Utils;


namespace Dependiator.Modeling
{
	[SingleInstance]
	internal class ModelService : IModelService
	{
		private readonly IMainViewItemsSource itemsSource;
		private readonly ICanvasService canvasService;
		private readonly IThemeService themeService;


		public ModelService(
			IMainViewItemsSource mainViewItemsSource,
			ICanvasService canvasService,
			IThemeService themeService)
		{
			this.itemsSource = mainViewItemsSource;
			this.canvasService = canvasService;
			this.themeService = themeService;
		}


		public void InitModules()
		{
			Timing t = new Timing();
			itemsSource.Add(GetModules());
			t.Log("Created modules");
		}


		private IEnumerable<Module> GetModules()
		{
			Random random = new Random();
			int total = 20;

			for (int y = 0; y < total; y++)
			{
				for (int x = 0; x < total; x++)
				{
					yield return new Module(canvasService)
					{
						RectangleBrush = themeService.GetNextBrush(),
						ItemBounds = new Rect(x * 100, y * 100, 60, 45),
						Priority = random.NextDouble()
					};
				}
			}
		}
	}
}