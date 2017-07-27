using System;
using System.Windows;
using System.Windows.Media;
using Dependinator.ModelViewing.Private.Items;

namespace Dependinator.ModelViewing.Links
{
	internal class LineViewModel : ItemViewModel
	{
		private readonly ILinkService linkService;
		private static readonly double NodeBorderThickness = 0.8 * 2;
		private static readonly double ArrowLength = 1.0;
		private static readonly int Margin = 30;

		private static readonly int xo1 = 110;
		private static readonly int yo1 = 200;
		private static readonly int xo2 = 330;
		private static readonly int yo2 = 220;

		private static readonly int xd1 = xo1 < xo2 ? 0 : 1;
		private static readonly int yd1 = yo1 < yo2 ? 0 : 1;
		private static readonly int xd2 = xo1 < xo2 ? 1 : 0;
		private static readonly int yd2 = yo1 < yo2 ? 1 : 0;


		private readonly int x = Math.Min(xo1, xo2);
		private readonly int y = Math.Min(yo1, yo2);
		private readonly int width = Math.Abs(xo2 - xo1);
		private readonly int height = Math.Abs(yo2 - yo1);
		


		public LineViewModel(
			ILinkService linkService)
		{
			this.linkService = linkService;

			ItemBounds = new Rect(x - Margin, y - Margin, width + Margin * 2, height + Margin * 2);
		}

		public double X1 => (width * xd1 + Margin) * ItemScale - NodeBorderThickness;
		public double Y1 => (height * yd1 + Margin) * ItemScale - NodeBorderThickness;
		public double X2 => (width * xd2 + Margin) * ItemScale - NodeBorderThickness;
		public double Y2 => (height * yd2 + Margin) * ItemScale - NodeBorderThickness;



		private double xfac => ((X2 - X1) / (Math.Abs(X2 - X1) + Math.Abs(Y2 - Y1))).MM(-0.9, 0.9);
		private double yfac => ((Y2 - Y1) / (Math.Abs(Y2 - Y1) + Math.Abs(X2 - X1))).MM(-0.9, 0.9);


		public double X21 => X2 - (ArrowThickness * xfac / 2) + (ArrowLength * 2 * xfac);
		public double Y21 => Y2 - (ArrowThickness * yfac / 2) + (ArrowLength * 2 * yfac);
		public double X22 => X2 - (ArrowThickness * xfac / 2) + (ArrowLength * 3 * xfac);
		public double Y22 => Y2 - (ArrowThickness * yfac / 2) + (ArrowLength * 3 * yfac);


		public double StrokeThickness => 1;
		public double ArrowThickness => (10 * ItemScale).MM(4, 10);



		public Brush LineBrush => GetLineBrush();

		public Brush LineBackgroundBrush => Brushes.Transparent;


		public bool IsMouseOver { get; private set; }


		private Brush GetLineBrush() => Brushes.Red;


		public Brush HoverBrush => Brushes.GhostWhite;

		public string StrokeDash => "";
		public string ToolTip => GetToolTip();

		public override string ToString() => "line";


		public void ToggleLine()
		{

		}


		private string GetToolTip()
		{

			//IReadOnlyList<LinkGroup> linkGroups = linkService.GetLinkGroups(linkLine);
			//string tip = "";

			//foreach (var group in linkGroups)
			//{
			//	tip += $"\n  {group.Source} -> {group.Target} ({group.Links.Count})";
			//}

			//int linksCount = linkLine.Links.Count;
			//tip = $"{linksCount} links:" + tip;

			//if (linkLine.Source == linkLine.Target.ParentNode
			//    || linkLine.Target == linkLine.Source.ParentNode)
			//{
			//	tip = $"{linksCount} links:" + tip;
			//}
			//else
			//{
			//	tip = $"{this} {linksCount} links:" + tip;
			//}

			return "Line";
		}


		public void ZoomLinks(double zoom, Point viewPosition)
		{
		}


		public void OnMouseEnter()
		{
			IsMouseOver = true;
			Notify(nameof(LineBrush), nameof(StrokeThickness));
		}


		public void OnMouseLeave()
		{
			IsMouseOver = false;
			Notify(nameof(LineBrush), nameof(StrokeThickness));
		}
	}
}