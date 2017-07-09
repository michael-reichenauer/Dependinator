using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependinator.Common.ThemeHandling;

namespace Dependinator.ModelViewing.Nodes.Private
{
	internal class NodeService : INodeService
	{
		private static readonly Size DefaultSize = new Size(200, 100);

		private readonly IThemeService themeService;


		public NodeService(
			IThemeService themeService)
		{
			this.themeService = themeService;
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


		public void SetChildrenLayout(NodeOld parent)
		{
			int rowLength = 6;

			int padding = 20;

			double xMargin = 10;
			double yMargin = 100;

			int count = 0;
			var children = parent.ChildNodes.OrderBy(child => child, NodeComparer.Comparer(parent));

			foreach (NodeOld childNode in children)
			{
				Size size;
				Point location;

				if (childNode.PersistentNodeBounds.HasValue)
				{
					size = childNode.PersistentNodeBounds.Value.Size;
					location = childNode.PersistentNodeBounds.Value.Location;
				}
				else
				{
					size = DefaultSize;
					double x = (count % rowLength) * (size.Width + padding) + xMargin;
					double y = (count / rowLength) * (size.Height + padding) + yMargin;
					location = new Point(x, y);
				}

				Rect bounds = new Rect(location, size);
				childNode.NodeBounds = bounds;
				count++;
			}
		}
	}
}
