using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Items;


namespace Dependiator.Modeling
{
	internal class LinkSegmentViewModel : ItemViewModel
	{
		private readonly LinkSegment linkSegment;

		public LinkSegmentViewModel(LinkSegment linkSegment)
		{
			this.linkSegment = linkSegment;
		}

		protected override Rect GetItemBounds() => linkSegment.GetItemBounds();


		public double X1 => linkSegment.L1.X * linkSegment.LinkScale;
		public double Y1 => linkSegment.L1.Y * linkSegment.LinkScale;
		public double X2 => linkSegment.L2.X * linkSegment.LinkScale;
		public double Y2 => linkSegment.L2.Y * linkSegment.LinkScale;
		public double StrokeThickness => linkSegment.LineThickness;

		public Brush LineBrush => linkSegment.LinkBrush;
		public Brush HoverBrush => Brushes.Transparent;
		public string StrokeDash { get; set; } = "";
		public string ToolTip => linkSegment.GetToolTip();

		public override string ToString() => linkSegment.ToString();
	}
}