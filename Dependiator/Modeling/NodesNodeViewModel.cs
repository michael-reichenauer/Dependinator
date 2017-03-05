using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Items;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.Modeling
{
	internal class NodesNodeViewModel : ItemViewModel
	{
		private readonly Node node;


		public NodesNodeViewModel(Node node)
			: base(node)
		{
			this.node = node;
			NodesViewModel = new NodesViewModel();
			NodesViewModel.Scale = node.NodeScale;
		}


		public NodesViewModel NodesViewModel { get; }

		public double StrokeThickness => 1;
		public Brush RectangleBrush => node.GetNodeBrush();
		public Brush BackgroundBrush => node.GetBackgroundNodeBrush();
		public Brush HoverBrush => RectangleBrush;


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

		public void AddItems(IEnumerable<Node> childNodes)
		{
			NodesViewModel.AddItems(childNodes);
		}


		public Task LoadAsync()
		{
			return Task.CompletedTask;
		}


		public void Zoom(int zoomDelta, Point viewPosition)
		{
			//node.Zoom(zoomDelta, viewPosition);
		}

		public void Resize(int zoomDelta, Point viewPosition)
		{
			// node.Resize(zoomDelta, viewPosition);
		}



		//internal void MouseMove(Point viewPosition, Vector viewOffset, bool isFirst)
		//{
		//	node.MoveOrResize(viewPosition, viewOffset, isFirst);
		//}
	}
}