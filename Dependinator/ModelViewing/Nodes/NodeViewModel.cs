using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.Common.ThemeHandling;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils.UI;

namespace Dependinator.ModelViewing.Nodes
{
	internal abstract class NodeViewModel : ItemViewModel
	{
		private readonly INodeViewModelService nodeViewModelService;

		private readonly DelayDispatcher mouseOverDelay = new DelayDispatcher();

		private int currentPointIndex = -1;
		private Point mouseDownPoint;
		private Point mouseMovedPoint;
		//private static readonly double MinControlScale = 0.5;


		protected NodeViewModel(INodeViewModelService nodeViewModelService, Node node)
		{
			this.nodeViewModelService = nodeViewModelService;
			this.Node = node;

			RectangleBrush = node.Color != null
				? Converter.BrushFromHex(node.Color)
				: nodeViewModelService.GetRandomRectangleBrush();
			BackgroundBrush = nodeViewModelService.GetRectangleBackgroundBrush(RectangleBrush);
		}


		public override bool CanShow => ItemScale > 0.15;

		public Node Node { get; }

		public Brush RectangleBrush { get; }
		public Brush BackgroundBrush { get; }


		public string Name => Node.Name.Name;

		public double RectangleLineWidth => IsMouseOver ? 0.6 * 1.5 : 0.6;

		public string ToolTip => $"{Node.Name}{DebugToolTip}";

		public void UpdateToolTip() => Notify(nameof(ToolTip));

		public int FontSize => ((int)(15 * ItemScale)).MM(9, 13);

		public bool IsMouseOver
		{
			get => Get();
			private set => Set(value).Notify(nameof(RectangleLineWidth));
		}

		public bool IsShowPoints { get => Get(); private set => Set(value); }

		public ItemsViewModel ItemsViewModel { get; set; }

		public override void ItemRealized()
		{
			base.ItemRealized();

			// If this node has an items canvas, make sure it knows it has been realized (fix zoom level)
			ItemsViewModel?.ItemRealized();
		}

		public override void ItemVirtualized()
		{
			base.ItemVirtualized();
			ItemsViewModel?.ItemVirtualized();
		}


		public string DebugToolTip => ItemsToolTip;


		public string ItemsToolTip =>
			"\n" +
			$"Rect: {ItemBounds.TS()}\n" +
			$"Scale {ItemScale}\n";

		public string Color => RectangleBrush.AsString();

		//$"Items: {ItemOwnerCanvas.CanvasRoot.AllItemsCount()}, Shown {ItemOwnerCanvas.CanvasRoot.ShownItemsCount()}";


		public void OnMouseEnter()
		{
			mouseOverDelay.Delay(ModelViewModel.MouseEnterDelay, _ =>
			{
				if (IsResizable())
				{
					IsShowPoints = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
				}
				IsMouseOver = true;
			});
		}

		public void OnMouseLeave()
		{
			mouseOverDelay.Cancel();
			IsShowPoints = false;
			IsMouseOver = false;
		}


		public void MouseDown(Point screenPoint)
		{
			mouseDownPoint = ItemOwnerCanvas.RootScreenToCanvasPoint(screenPoint);
			mouseMovedPoint = mouseDownPoint;
			currentPointIndex = -1;
		}


		public void MouseMove(Point screenPoint)
		{
			Point point = ItemOwnerCanvas.RootScreenToCanvasPoint(screenPoint);

			if (currentPointIndex == -1)
			{
				// First move event, lets start a move by  getting the index of point to move.
				// THis might create a new point if there is no existing point near the mouse down point
				currentPointIndex = nodeViewModelService.GetPointIndex(Node, mouseDownPoint);
				if (currentPointIndex == -1)
				{
					// Point not close enough to the line
					return;
				}

				if (!IsResizable())
				{
					// Only support move if scale is small
					currentPointIndex = 0;
				}
			}

			nodeViewModelService.MovePoint(Node, currentPointIndex, point, mouseMovedPoint);
			mouseMovedPoint = point;
			IsMouseOver = true;
			if (IsResizable())
			{
				IsShowPoints = true;
			}

			Node.ItemsCanvas?.UpdateAndNotifyAll();

			NotifyAll();
		}


		private bool IsResizable()
		{
			return ItemWidth * ItemScale > 70 || ItemHeight * ItemScale > 70;
		}


		public void MouseUp(Point screenPoint) => currentPointIndex = -1;

		public override string ToString() => Node.Name.ToString();
	}
}