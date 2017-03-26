using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Items;
using Dependiator.Utils;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.Modeling
{
	internal class CompositeNodeViewModel : ItemViewModel
	{
		private readonly Node node;

		public CompositeNodeViewModel(Node node, ItemsCanvas parentCanvas)
		{
			this.node = node;
			NodesViewModel = new NodesViewModel(this, parentCanvas);
			NodesViewModel.Scale = node.NodeScale;
		}


		public NodesViewModel NodesViewModel { get; }

		public double ScaleFactor { get; private set; } = 1;

		public ItemsCanvas ItemsCanvas => NodesViewModel.ItemsCanvas;

		public double StrokeThickness => 0.8;
		public Brush RectangleBrush => node.GetNodeBrush();
		public Brush BackgroundBrush => node.GetBackgroundNodeBrush();
		public Brush HoverBrush => RectangleBrush;

		public NodesView ParentView => node.ParentView;

		public string Name => node.NodeName.ShortName;

		public int CornerRadius => 3;

		public string ToolTip =>
			$"{node.NodeName} ({node.ChildNodes.Count})\n" +
			$"Scale: {node.NodeScale:0.00} NSF: {node.ScaleFactor}, Items Total: {TotalCount}, Instance: {InstanceCount}" +
			$"\nParentScale: {node.ParentNode.NodeScale:0.00}, Node Canvas Scale: {Scale:0.00}";


		public override void ItemRealized()
		{
			base.ItemRealized();
			node.NodeRealized();
		}


		public override void ItemVirtualized()
		{
			node.NodeVirtualized();
			base.ItemVirtualized();
		}

		public int FontSize
		{
			get
			{
				int fontSize = (int)(12 * node.NodeScale * 10);
				return fontSize.MM(8, 20);
			}
		}

		public double Scale => ItemsCanvas.Scale;


		public override Rect GetItemBounds() => node.ItemBounds;


		public void UpdateScale()
		{
			NodesViewModel.Scale = node.NodeScale / ScaleFactor;
			NotifyAll();
		}


		public Task LoadAsync()
		{
			return Task.CompletedTask;
		}


		public void Zoom(int zoomDelta, Point viewPosition)
		{
			ItemsCanvas.Zoom(zoomDelta, viewPosition);
			ScaleFactor = node.NodeScale / ItemsCanvas.Scale;
		}

		public void Resize(int zoomDelta, Point viewPosition) => node.Resize(zoomDelta, viewPosition);

		public void MoveNode(Vector viewOffset) => node.MoveNode(viewOffset);

		//internal void MouseMove(Point viewPosition, Vector viewOffset, bool isFirst)
		//{
		//	node.MoveOrResize(viewPosition, viewOffset, isFirst);
		//}



		public override string ToString() => node.NodeName;
		public override double GetScaleFactor() => node.ScaleFactor;



	}
}