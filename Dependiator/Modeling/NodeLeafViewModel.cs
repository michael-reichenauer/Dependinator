using System;
using System.Threading.Tasks;
using System.Windows.Media;
using Dependiator.MainViews;


namespace Dependiator.Modeling
{
	internal class NodeLeafViewModel : ItemViewModel
	{
		public NodeLeafViewModel(NodeItem node)
			: base(node)
		{
		}


		public double StrokeThickness => 1;
		public Brush RectangleBrush => Brushes.Aquamarine;
		public Brush HoverBrush => Brushes.Red;

		public Brush BackgroundBrush => Brushes.Transparent;

		public string Name => "NodeLeaf";

		public int CornerRadius => 0;

		public string ToolTip => "Node";


		public int FontSize => 10;
	}
}