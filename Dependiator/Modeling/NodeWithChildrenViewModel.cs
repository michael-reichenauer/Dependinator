using System;
using System.Threading.Tasks;
using System.Windows.Media;
using Dependiator.Modeling.Items;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.Modeling
{
	internal class NodeWithChildrenViewModel : ItemViewModel
	{
		private readonly Node node;

		public NodeWithChildrenViewModel(Node node)
			: base(node)
		{
			this.node = node;
			NodesViewModel = new NodesViewModel();
			NodesViewModel.Scale = node.NodeScale;
		}

		public NodesViewModel NodesViewModel { get; }

		public double StrokeThickness => 1;
		public Brush RectangleBrush => node.RectangleBrush;
		public Brush HoverBrush => node.RectangleBrush;

		public Brush BackgroundBrush => node.BackgroundBrush;

		public string Name => node.NodeName.ShortName;

		public int CornerRadius => 0;

		public string ToolTip =>
			$"{node.NodeName}\nScale: {node.NodeScale:0.00} NSF: {node.ScaleFactor}";



		public int FontSize
		{
			get
			{
				int fontSize = (int)(12 * node.NodeScale);
				return fontSize.MM(8, 20);
			}
		}


		public void UpdateZoomScale()
		{
			NodesViewModel.Scale = node.NodeScale;
			NotifyAll();
		}


		public Task LoadAsync()
		{
			return Task.CompletedTask;
		}


		//public void Add(Node childNode)
		//{
		//	NodesViewModel.AddItem(childNode);
		//}


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


	}
}