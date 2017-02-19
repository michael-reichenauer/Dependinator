using System;
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


		public double X1 => link.X1 * link.CanvasScale;
		public double Y1 => link.Y1 * link.CanvasScale;
		public double X2 => link.X2 * link.CanvasScale;
		public double Y2 => link.Y2 * link.CanvasScale;
		public double StrokeThickness => link.LineThickness;

		public Brush LineBrush => link.LinkBrush;
		public Brush HoverBrush => Brushes.Transparent;
		public string StrokeDash { get; set; } = "";
		public string ToolTip => link.ToolTip;
	}
}