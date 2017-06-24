using System;
using System.Windows;
using System.Windows.Media;
using Dependinator.Modeling.Items;


namespace Dependinator.Modeling.Nodes
{
	internal class MemberNodeViewModel : ItemViewModel
	{
		private readonly Node node;


		public MemberNodeViewModel(Node node)
		{
			this.node = node;
		}

		protected override Rect GetItemBounds() => node.NodeBounds;

		public double StrokeThickness => 0.8;
		public Brush RectangleBrush => node.GetNodeBrush();
		public Brush BackgroundBrush => node.GetBackgroundNodeBrush();
		public Brush HoverBrush => RectangleBrush;

		public string Name => node.NodeName.ShortName;

		public string ToolTip => $"{node.NodeName}{node.DebugToolTip}";


		public int CornerRadius => 3;


		public int FontSize => ((int)(15 * node.NodeScale * 1.5)).MM(8, 15);


		public override string ToString() => node.NodeName;


		public void MoveNode(Vector viewOffset) => node.Move(viewOffset, null, false);

		public void Resize(int zoomDelta) => node.Resize(zoomDelta);
	}
}