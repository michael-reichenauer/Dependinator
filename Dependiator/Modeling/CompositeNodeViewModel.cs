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

		public double StrokeThickness => 1;
		public Brush RectangleBrush => node.GetNodeBrush();
		public Brush BackgroundBrush => node.GetBackgroundNodeBrush();
		public Brush HoverBrush => RectangleBrush;


		public string Name => node.NodeName.ShortName;

		public int CornerRadius => 0;

		public string ToolTip =>
			$"{node.NodeName} ({node.ChildNodes.Count})\n" +
			$"Scale: {node.NodeScale:0.00} NSF: {node.ScaleFactor}, Items: {ItemsCount}, {ItemsSource.ItemCount}" +
			$"\nParentScale: {node.ParentNode.NodeScale:0.00}, Node Canvas Scale: {Scale:0.00}";


		public override void ItemRealized()
		{
			base.ItemRealized();
			node.ShowAllChildren();
		}


		public override void ItemVirtualized()
		{
			node.HideAllChildren();
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

		public void Resize(int zoomDelta, Point viewPosition)
		{
			// node.Resize(zoomDelta, viewPosition);
		}


		//internal void MouseMove(Point viewPosition, Vector viewOffset, bool isFirst)
		//{
		//	node.MoveOrResize(viewPosition, viewOffset, isFirst);
		//}

		public void AddItem(IItem item)
		{
			ItemsCanvas.AddItem(item);
		}


		public void RemoveItem(IItem item)
		{
			Log.Debug($"{item}");
			ItemsCanvas.RemoveItem(item);
		}


		public bool MoveCanvas(Vector viewOffset)
		{
			NodesViewModel.MoveCanvas(viewOffset);
			return true;
		}


		public override string ToString() => node.NodeName;
		public override double GetScaleFactor() => node.ScaleFactor;
	}
}