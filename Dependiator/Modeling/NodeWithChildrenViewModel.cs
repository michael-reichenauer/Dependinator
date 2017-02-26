using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Dependiator.MainViews;
using Dependiator.MainViews.Private;
using Dependiator.Utils.UI;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.Modeling
{
	internal class NodeWithChildrenViewModel : ItemViewModel
	{
		private readonly Node node;

		private ZoomableCanvas canvas;

		public VirtualItemsSource ItemsSource { get; }

		private INodeItemsSource nodeItemsSource;

		public NodeWithChildrenViewModel(Node node)
			: base(node)
		{
			this.node = node;

			nodeItemsSource = new NodeItemsSource();
			ItemsSource = nodeItemsSource.VirtualItemsSource;

			nodeItemsSource.Add(new NodeItem(new Rect(10, 10, 70, 40)));

			StrokeThickness = 1;
			
		}


		public double StrokeThickness { get; }
		public double RectangleWidth => node.ItemCanvasBounds.Width * node.CanvasScale - StrokeThickness * 2;
		public double RectangleHeight => node.ItemCanvasBounds.Height * node.CanvasScale - StrokeThickness * 2;
		public Brush RectangleBrush => node.RectangleBrush;
		public Brush HoverBrush => node.RectangleBrush;

		public Brush BackgroundBrush => node.BackgroundBrush;

		public string Name => node.ItemViewSize.Width > 40 ? node.NodeName.ShortName : " ";

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


		public void SetCanvas(ZoomableCanvas zoomableCanvas)
		{
			canvas = zoomableCanvas;
		}


		public Task LoadAsync()
		{
			return Task.CompletedTask;
		}
	}
}