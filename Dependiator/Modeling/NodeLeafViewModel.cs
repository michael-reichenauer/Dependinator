using System;
using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Items;


namespace Dependiator.Modeling
{
	internal class NodeLeafViewModel : ItemViewModel
	{
		private readonly Node node;


		public NodeLeafViewModel(Node node)
		{
			this.node = node;
		}

		public override Rect GetItemBounds() => node.ItemBounds;


		public double StrokeThickness => 1;
		public Brush RectangleBrush => node.GetNodeBrush();
		public Brush BackgroundBrush => node.GetBackgroundNodeBrush();
		public Brush HoverBrush => RectangleBrush;


		public string Name => node.NodeName.ShortName;

		public string ToolTip =>
			$"{node.NodeName} ({node.ChildNodes.Count})\n" + 
			$"Scale: {node.NodeScale:0.00} NSF: {node.ScaleFactor}, Items: {ItemsCount}, {ItemsSource.ItemCount}" +
			$"\nParentScale: {node.ParentNode.NodeScale:0.00}";


		public int CornerRadius => 10;


		public int FontSize
		{
			get
			{
				int fontSize = (int)(12 * node.NodeScale * 10);
				return fontSize.MM(8, 20);
			}
		}

		public override string ToString() => node.NodeName;
		public override double GetScaleFactor() => node.ScaleFactor;

	}
}