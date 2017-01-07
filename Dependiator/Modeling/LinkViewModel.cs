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

		public string ToolTip => $"Count {link.Reference.SubReferences.Count} {link.Reference}";
		public int StrokeThickness => 1;
		//public string Line => $"M {x1},{y1} L {x2},{y2}";
		public Brush LineBrush => link.LinkBrush;
		public Brush HoverBrush => Brushes.Transparent;
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