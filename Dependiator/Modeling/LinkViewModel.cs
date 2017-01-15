using System.Windows.Media;
using Dependiator.MainViews;


namespace Dependiator.Modeling
{
	internal class LinkViewModel : ItemViewModel
	{
		private readonly Link link;

		public LinkViewModel(Link link)
			: base(link)
		{
			this.link = link;
		}



		public double X1 => link.X1 * link.ParentNode.ViewScale;
		public double Y1 => link.Y1 * link.ParentNode.ViewScale;
		public double X2 => link.X2 * link.ParentNode.ViewScale;
		public double Y2 => link.Y2 * link.ParentNode.ViewScale;

		public string ToolTip => link.ToolTip;
		public Brush LineBrush => link.LinkBrush;
		public Brush HoverBrush => Brushes.Transparent;
		public string StrokeDash { get; set; } = "";

		public int StrokeThickness
		{
			get
			{
				if (link.SubLinkCount < 5)
				{
					return 1;
				}
				else if (link.SubLinkCount < 15)
				{
					return 2;
				}
				else 
				{
					return 3;
				}
			}
		}
	}
}