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

			ItemsCanvas = new ItemsCanvas(this, parentCanvas);
			NodesViewModel = new NodesViewModel(ItemsCanvas);
		}


		public NodesViewModel NodesViewModel { get; }

		public ItemsCanvas ItemsCanvas { get; }

		public double Scale => ItemsCanvas.Scale;



		public double StrokeThickness => 0.8;
		public Brush RectangleBrush => node.GetNodeBrush();
		public Brush BackgroundBrush => node.GetBackgroundNodeBrush();
		public Brush HoverBrush => RectangleBrush;

		public NodesView ParentView => node.ParentView;

		public string Name => node.NodeName.ShortName;

		public int CornerRadius => 3;

		public string ToolTip => $"{node.NodeName}{node.ToolTip}";


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

		public int FontSize => ((int)(15 * node.NodeItemScale)).MM(8, 15);

		//public int FontSize => 15;
		//{
		//	get
		//	{
		//		int fontSize = (int)(12 * node.NodeItemScale * 10);
		//		return fontSize.MM(8, 20);
		//	}
		//}



		public override Rect GetItemBounds() => node.ItemBounds;


		public void UpdateScale()
		{
			ItemsCanvas.UpdateScale();
			NotifyAll();
		}


		public Task LoadAsync()
		{
			return Task.CompletedTask;
		}


		public void Zoom(int zoomDelta, Point viewPosition)
		{
			node.Zoom(zoomDelta, viewPosition);
		}

		public void Resize(int zoomDelta, Point viewPosition) => node.Resize(zoomDelta, viewPosition);

		public void MoveNode(Vector viewOffset) => node.MoveNode(viewOffset);


		public override string ToString() => node.NodeName;
	}
}