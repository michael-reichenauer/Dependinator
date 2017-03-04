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


		//public VirtualItemsSource ItemsSource => node.VirtualItemsSource;
		private ItemsCanvas canvas = new ItemsCanvas();

		public NodeWithChildrenViewModel(Node node)
			: base(node)
		{
			this.node = node;		
		}


		public double StrokeThickness => 1;
		public Brush RectangleBrush => node.RectangleBrush;
		public Brush HoverBrush => node.RectangleBrush;

		public Brush BackgroundBrush => node.BackgroundBrush;

		public string Name => node.NodeName.ShortName;

		public int CornerRadius => 0;

		public string ToolTip => $"{node.NodeName}";
		//$"{node.NodeName}\nScale: {node.CanvasScale:#.##}, Level: {node.ItemLevel}, " +
		//$"NodeScale: {node.ItemScale:#.##}, NSF: {node.ThisItemScaleFactor}";


		public int FontSize
		{
			get
			{
				int fontSize = (int)(12 * node.NodeScale);
				return fontSize.MM(8, 20);
			}
		}


//internal void MouseMove(Point viewPosition, Vector viewOffset, bool isFirst)
//{
//	node.MoveOrResize(viewPosition, viewOffset, isFirst);
//}

//public void Zoom(int zoomDelta, Point viewPosition)
//{
//	//node.Zoom(zoomDelta, viewPosition);
//}

//public void Resize(int zoomDelta, Point viewPosition)
//{
//	node.Resize(zoomDelta, viewPosition);
//}


		public void SetCanvas(ZoomableCanvas zoomableCanvas)
		{
			canvas.SetCanvas(zoomableCanvas);
			UpdateZoomScale();
		}


		public void UpdateZoomScale()
		{
			if (canvas != null)
			{
				canvas.Scale = node.NodeScale;
			}
		}


		public Task LoadAsync()
		{
			return Task.CompletedTask;
		}


		public void Add(Node childNode)
		{
			canvas.AddItem(childNode);
		}
	}
}