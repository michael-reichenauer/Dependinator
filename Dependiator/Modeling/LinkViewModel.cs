using System;
using System.Windows.Forms;
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
				if (link.SubLinkCount < 3)
				{
					return 1;
				}
				else if (link.SubLinkCount < 7)
				{
					return 2;
				}
				else if (link.SubLinkCount < 15)
				{
					return 3;
				}
				else
				{
					return 4;
				}
			}
		}
	}
}