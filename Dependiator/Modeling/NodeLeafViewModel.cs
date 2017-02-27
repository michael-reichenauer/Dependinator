using System;
using System.Threading.Tasks;
using System.Windows.Media;
using Dependiator.MainViews;


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
		public Brush RectangleBrush => node.RectangleBrush;
		public Brush HoverBrush => node.RectangleBrush;

		public Brush BackgroundBrush => Brushes.Transparent;

		public string Name => "NodeLeaf";

		public int CornerRadius => 0;

		public string ToolTip => "Node";


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