using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Items;


namespace Dependiator.Modeling
{
	internal class LinkViewModel : ItemViewModel
	{
		private readonly Link link;

		public LinkViewModel(Link link)
		{
			this.link = link;
		}

		protected override Rect GetItemBounds() => link.ItemBounds;


		public double X1 => link.X1 * link.LinkScale;
		public double Y1 => link.Y1 * link.LinkScale;
		public double X2 => link.X2 * link.LinkScale;
		public double Y2 => link.Y2 * link.LinkScale;
		public double StrokeThickness => link.LineThickness;

		public Brush LineBrush => link.LinkBrush;
		public Brush HoverBrush => Brushes.Transparent;
		public string StrokeDash { get; set; } = "";
		public string ToolTip => link.ToolTip;
	}
}