using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.Private;


namespace Dependinator.ModelViewing.Nodes
{
	internal class NodeViewModel : ItemViewModel
	{
		private Point lastMousePosition;
		private readonly Node node;

		protected NodeViewModel(Node node)
		{
			this.node = node;
		}

		protected override Rect GetItemBounds() => node.NodeBounds;


		public void OnMouseMove(MouseEventArgs e)
		{
			Point viewPosition = e.GetPosition(Application.Current.MainWindow);

			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
					&& e.LeftButton == MouseButtonState.Pressed
					&& !(e.OriginalSource is Thumb)) // Don't block the scrollbars.
			{

				// Move node
				(e.Source as UIElement)?.CaptureMouse();
				Vector viewOffset = viewPosition - lastMousePosition;
				e.Handled = true;

				node.Move(viewOffset, null, false);
			}
			else
			{
				// End of move
				(e.Source as UIElement)?.ReleaseMouseCapture();
			}

			lastMousePosition = viewPosition;
		}

		public override string ToString() => node.NodeName;
	}


	internal class CompositeNodeViewModel : NodeViewModel
	{
		private readonly Node node;


		public CompositeNodeViewModel(IModelService modelService, Node node, ItemsCanvas itemsCanvas)
			: base(node)
		{
			this.node = node;
			ItemsCanvas = itemsCanvas;
			ModelViewModel = new ModelViewModel(modelService, node, ItemsCanvas);
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



		public void ResizeeNode(Vector viewOffset, Point viewPosition2) => node.Resize(viewOffset, viewPosition2);




		public void UpdateToolTip()
		{
			Notify(nameof(ToolTip));
		}
	}
}