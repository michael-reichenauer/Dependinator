using System;
using System.Windows;
using System.Windows.Media;
using Dependinator.ModelViewing.Items;


namespace Dependinator.ModelViewing.Nodes
{
	internal class CompositeNodeViewModel : ItemViewModel
	{
		private readonly Node node;


		public CompositeNodeViewModel(IModelViewService modelViewService, Node node, ItemsCanvas itemsCanvas)
		{
			this.node = node;
			ItemsCanvas = itemsCanvas;
			ModelViewModel = new ModelViewModel(modelViewService, node, ItemsCanvas);
		}


		protected override Rect GetItemBounds() => node.NodeBounds;


		public ModelViewModel ModelViewModel { get; }

		public ItemsCanvas ItemsCanvas { get; }

		public double Scale => ItemsCanvas.Scale;


		public double StrokeThickness => 0.8;
		public Brush RectangleBrush => node.GetNodeBrush();
		public Brush BackgroundBrush => node.GetBackgroundNodeBrush();
		public Brush HoverBrush => RectangleBrush;

		public string Name => node.NodeName.ShortName;

		public int CornerRadiusX => node.NodeType == NodeType.TypeType ? 10 : 0;
		public int CornerRadiusY => node.NodeType == NodeType.TypeType ? 10 : 0;
		public string StrokeDash => node.NodeType == NodeType.TypeType ? "" : "4,6";

		public string ScrollDash => node.NodeType == NodeType.TypeType ? "" : "4,6";

		public string ToolTip => $"{node.NodeName}{node.DebugToolTip}";


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


		public int FontSize => ((int)(25 * node.NodeScale)).MM(8, 13);






		public void UpdateScale()
		{
			ItemsCanvas.UpdateScale();
			NotifyAll();
		}



		public void Zoom(double zoomFactor, Point viewPosition) => node.Zoom(zoomFactor, viewPosition);

		public void ZoomResize(int wheelDelta) => node.Resize(wheelDelta);


		public void MoveNode(Vector viewOffset, Point viewPosition2, bool isDoing) =>
			node.Move(viewOffset, viewPosition2, isDoing);


		public void ResizeeNode(Vector viewOffset, Point viewPosition2) => node.Resize(viewOffset, viewPosition2);


		public override string ToString() => node.NodeName;


		public void UpdateToolTip()
		{
			Notify(nameof(ToolTip));
		}
	}
}