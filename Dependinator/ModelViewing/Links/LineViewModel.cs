using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Dependinator.ModelViewing.Private.Items;

namespace Dependinator.ModelViewing.Links
{
	internal class LineViewModel : ItemViewModel
	{
		private readonly ILinkService linkService;
		private readonly ItemViewModel source;
		private readonly ItemViewModel target;
		private static readonly double NodeMargin = 0.8 * 2;
		private static readonly double ArrowLength = 1.0;
		private static readonly double LineMargin = 30;

		// Line and arrow variables for boundary, position and direction
		private double x;
		private double y;
		private double width;
		private double height;
		private int xf;
		private int yf;
		private int xd1;
		private int yd1;
		private int xd2;
		private int yd2;
		private double arrowXd;
		private double arrowYd;


		public LineViewModel(
			ILinkService linkService,
			ItemViewModel source,
			ItemViewModel target)
		{
			this.linkService = linkService;
			this.source = source;
			this.target = target;

			Point sp = source.ItemBounds.Location;
			Point tp = target.ItemBounds.Location;
			SetLine(sp, tp);

			PropertyChangedEventManager.AddHandler(
				source, SourceOrTargetChanged, nameof(source.ItemBounds));
			PropertyChangedEventManager.AddHandler(
				target, SourceOrTargetChanged, nameof(source.ItemBounds));
		}

		private void SourceOrTargetChanged(object sender, PropertyChangedEventArgs e)
		{
			Point sp = source.ItemBounds.Location;
			Point tp = target.ItemBounds.Location;
			SetLine(sp, tp);
			NotifyAll();
		}



		// The line endpoints (x1,y1)->(x2,y2) within the item bounds with margins and direction
		public double X1 => (width * xd1 + LineMargin) * ItemScale - NodeMargin;
		public double Y1 => (height * yd1 + LineMargin) * ItemScale - NodeMargin;
		public double X2 => (width * xd2 + LineMargin) * ItemScale - NodeMargin;
		public double Y2 => (height * yd2 + LineMargin) * ItemScale - NodeMargin;
		public double LineWidth => 1;

		// The arrow line endpoint (based on the line end (x2,y2) and with size and direction
		public double ArrowX1 => X2 - (ArrowWidth * arrowXd / 2) + (ArrowLength * 2 * arrowXd);
		public double ArrowY1 => Y2 - (ArrowWidth * arrowYd / 2) + (ArrowLength * 2 * arrowYd);
		public double ArrowX2 => X2 - (ArrowWidth * arrowXd / 2) + (ArrowLength * 3 * arrowXd);
		public double ArrowY2 => Y2 - (ArrowWidth * arrowYd / 2) + (ArrowLength * 3 * arrowYd);
		public double ArrowWidth => (10 * ItemScale).MM(4, 10);

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


		private void SetLine(Point source, Point target)
		{
			// Calculate the line boundaries (x, y, width, height)
			x = Math.Min(source.X, target.X);
			y = Math.Min(source.Y, target.Y);
			width = Math.Abs(target.X - source.X);
			height = Math.Abs(target.Y - source.Y);

			// The items bound has some margin around the line to allow full line width and arrow to show
			ItemBounds =
				new Rect(x - LineMargin, y - LineMargin, width + LineMargin * 2, height + LineMargin * 2);

			// Within the item bounds, the line will be drawn, and to do that we need some directions
			xf = source.X < target.X ? 1 : -1;
			yf = source.Y < target.Y ? 1 : -1;
			xd1 = source.X < target.X ? 0 : 1;
			yd1 = source.Y < target.Y ? 0 : 1;
			xd2 = source.X < target.X ? 1 : 0;
			yd2 = source.Y < target.Y ? 1 : 0;

			// The direction of the arrow head depend on the direction of the line
			arrowXd = (xf * width / (width + height)).MM(-0.9, 0.9);
			arrowYd = (yf * height / (height + width)).MM(-0.9, 0.9);
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

			return $"{source}->{target}";
		}


		public void ZoomLinks(double zoom, Point viewPosition)
		{
		}


		public void OnMouseEnter()
		{
			IsMouseOver = true;
			Notify(nameof(LineBrush), nameof(LineWidth));
		}


		public void OnMouseLeave()
		{
			IsMouseOver = false;
			Notify(nameof(LineBrush), nameof(LineWidth));
		}
	}
}