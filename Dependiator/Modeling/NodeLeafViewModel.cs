using System;
using System.Windows.Media;
using Dependiator.Modeling.Items;


namespace Dependiator.Modeling
{
	internal class NodeLeafViewModel : ItemViewModel
	{
		private readonly Node node;


		public NodeLeafViewModel(Node node)
			: base(node)
		{
			this.node = node;
		}


		public double StrokeThickness => 1;
		public Brush RectangleBrush => node.GetNodeBrush();
		public Brush BackgroundBrush => node.GetBackgroundNodeBrush();
		public Brush HoverBrush => RectangleBrush;


		public string Name => node.NodeName.ShortName;

		public string ToolTip =>
			$"{node.NodeName} ({node.ChildNodes.Count})\nScale: {node.NodeScale:0.00} NSF: {node.ScaleFactor}";


		public int CornerRadius => 10;




		public int FontSize
		{
			get
			{
				int fontSize = (int)(12 * node.NodeScale);
				return fontSize.MM(8, 20);
			}
		}
	}
}