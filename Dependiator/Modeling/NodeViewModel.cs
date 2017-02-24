using System;
using System.Windows;
using System.Windows.Media;
using Dependiator.MainViews;
using Dependiator.Modeling.Analyzing;


namespace Dependiator.Modeling
{
	internal class NodeViewModel : ItemViewModel
	{
		private readonly Node node;

		public NodeViewModel(Node node)
			: base(node)
		{
			this.node = node; 

			if (node.NodeType == NodeType.MemberType)
			{
				StrokeThickness = 0.5;
			}
			else if (node.NodeType == NodeType.TypeType)
			{
				StrokeThickness = 2;
			}
			else
			{
				StrokeThickness = 1;
			}
		}


		public double StrokeThickness { get; }
		public double RectangleWidth => node.ItemCanvasBounds.Width * node.CanvasScale - StrokeThickness * 2;
		public double RectangleHeight => node.ItemCanvasBounds.Height * node.CanvasScale - StrokeThickness * 2;
		public Brush RectangleBrush => node.RectangleBrush;
		public Brush HoverBrush => node.RectangleBrush;

		public Brush BackgroundBrush => node.BackgroundBrush;

		public string Name => node.ItemViewSize.Width > 40 ? node.Name : " ";

		public int CornerRadius => node.NodeType == NodeType.TypeType
			? (int)(node.ItemScale * 10).MM(0, 30)
			: 0;

		public string ToolTip =>
			$"{node.NodeName}\nScale: {node.CanvasScale:#.##}, Level: {node.ItemLevel}, " +
			$"NodeScale: {node.ItemScale:#.##}, NSF: {node.ThisItemScaleFactor}";


		public int FontSize
		{
			get
			{
				int fontSize = (int)(12 * node.ItemScale);
				return fontSize.MM(8, 20);
			}
		}


		internal void MouseMove(Point viewPosition, Vector viewOffset, bool isFirst)
		{
			node.MoveOrResize(viewPosition, viewOffset, isFirst);
		}

		public void Zoom(int zoomDelta, Point viewPosition)
		{
			//node.Zoom(zoomDelta, viewPosition);
		}

		public void Resize(int zoomDelta, Point viewPosition)
		{
			node.Resize(zoomDelta, viewPosition);
		}
	}
}