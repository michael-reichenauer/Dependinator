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


		private double x1 => 0;
		private double y1 => 0;
		private double x2 => CanvasWidth * link.NodeScaleFactor / link.NodeScale;
		private double y2 => CanvasHeight * link.NodeScaleFactor  / link.NodeScale;

		public string ToolTip => $"Count {link.Reference.SubReferences.Count} {link.Reference}";
		public int StrokeThickness => 1;
		public string Line => $"M {x1},{y1} L {x2},{y2}";
		public Brush LineBrush => link.LinkBrush;
		public Brush HoverBrush => link.LinkBrush;
		public string StrokeDash { get; set; } = "";



		//public int FontSize
		//{
		//	get
		//	{
		//		int fontSize = (int)(12 * module.Scale * module.NodeScale);
		//		fontSize = Math.Max(8, fontSize);
		//		return Math.Min(20, fontSize);
		//	}
		//}

	}
}