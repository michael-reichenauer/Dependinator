using System;
using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Items;


namespace Dependiator.Modeling
{
	internal class SingleNodeViewModel : ItemViewModel
	{
		private readonly Node node;


		public SingleNodeViewModel(Node node)
		{
			this.node = node;
		}

		public override Rect GetItemBounds() => node.ItemBounds;


		public double StrokeThickness => 0.8;
		public Brush RectangleBrush => node.GetNodeBrush();
		public Brush BackgroundBrush => node.GetBackgroundNodeBrush();
		public Brush HoverBrush => RectangleBrush;

		public NodesView ParentView => node.ParentView;
		public string Name => node.NodeName.ShortName;

		public string ToolTip =>$"{node.NodeName}{node.ToolTip}";


		public int CornerRadius => 3;


		public int FontSize
		{
			get
			{
				int fontSize = (int)(14 * node.NodeItemScale);
				return fontSize.MM(8, 15);
			}
		}

		public override string ToString() => node.NodeName;


		public void MoveNode(Vector viewOffset) => node.MoveNode(viewOffset);

		public void Resize(int zoomDelta, Point viewPosition) => node.Resize(zoomDelta, viewPosition);

	}
}