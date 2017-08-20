using System.Drawing;
using System.Globalization;
using System.Windows;
using Dependinator.Common.ThemeHandling;
using Dependinator.ModelViewing.Private;
using Brush = System.Windows.Media.Brush;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace Dependinator.ModelViewing.Nodes.Private
{
	internal class NodeViewModelService : INodeViewModelService
	{
		private static readonly Size DefaultSize = new Size(200, 100);
		private static readonly Rect None = new Rect(0, 0, 0, 0);

		private readonly IThemeService themeService;
		private readonly Model model;


		public NodeViewModelService(
			IThemeService themeService,
			Model model)
		{
			this.themeService = themeService;
			this.model = model;
		}


		public Brush GetRandomRectangleBrush()
		{
			return themeService.GetRectangleBrush();
		}

		public Brush GetBrushFromHex(string hexColor)
		{
			return themeService.GetBrushFromHex(hexColor);
		}

		public string GetHexColorFromBrush(Brush brush)
		{
			return themeService.GetHexColorFromBrush(brush);
		}

		public Brush GetRectangleBackgroundBrush(Brush brush)
		{
			return themeService.GetRectangleBackgroundBrush(brush);
		}

		public Brush GetRectangleHighlightBrush(Brush brush)
		{
			return themeService.GetRectangleHighlighterBrush(brush);
		}

		public void SetLayout(NodeViewModel nodeViewMode)
		{
			if (nodeViewMode.Node.Bounds != None)
			{
				nodeViewMode.ItemBounds = nodeViewMode.Node.Bounds;
				return;
			}

			int rowLength = 6;

			int padding = 20;

			double xMargin = 10;
			double yMargin = 100;

			Size size = DefaultSize;

			int siblingCount = nodeViewMode.Node.Parent.Children.Count - 1;

			double x = (siblingCount % rowLength) * (size.Width + padding) + xMargin;
			double y = (siblingCount / rowLength) * (size.Height + padding) + yMargin;
			Point location = new Point(x, y);

			Rect bounds = new Rect(location, size);

			nodeViewMode.ItemBounds = bounds;
		}
	}
}
