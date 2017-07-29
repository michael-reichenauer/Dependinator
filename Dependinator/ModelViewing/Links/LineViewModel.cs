using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Private.Items;

namespace Dependinator.ModelViewing.Links
{
	internal class LineViewModel : ItemViewModel
	{
		private readonly ILineViewModelService lineViewModelService;
		private readonly NodeViewModel source;
		private readonly NodeViewModel target;
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
			ILineViewModelService lineViewModelService,
			NodeViewModel source,
			NodeViewModel target)
		{
			this.lineViewModelService = lineViewModelService;
			this.source = source;
			this.target = target;
			ItemZIndex = -1;

			SetLine();
			TrackSourceOrTargetChanges();
		}

		public override bool CanShow => source.CanShow & target.CanShow;


		// The line endpoints (x1,y1)->(x2,y2) within the item bounds with margins and direction
		public double X1 => (width * xd1 + LineMargin) * ItemScale - NodeMargin;
		public double Y1 => (height * yd1 + LineMargin) * ItemScale - NodeMargin;
		public double X2 => (width * xd2 + LineMargin) * ItemScale - NodeMargin;
		public double Y2 => (height * yd2 + LineMargin) * ItemScale - NodeMargin;
		public double LineWidth => (1 * ItemScale).MM(0.2, 2);

		// The arrow line endpoint (based on the line end (x2,y2) and with size and direction
		public double ArrowX1 => X2 - (ArrowWidth * arrowXd / 2) + (ArrowLength * 2 * arrowXd);
		public double ArrowY1 => Y2 - (ArrowWidth * arrowYd / 2) + (ArrowLength * 2 * arrowYd);
		public double ArrowX2 => X2 - (ArrowWidth * arrowXd / 2) + (ArrowLength * 3 * arrowXd);
		public double ArrowY2 => Y2 - (ArrowWidth * arrowYd / 2) + (ArrowLength * 3 * arrowYd);
		public double ArrowWidth => (10 * ItemScale).MM(4, 10);

		public Brush LineBrush => source.RectangleBrush;
		public Brush LineBackgroundBrush => Brushes.Transparent;


		public bool IsMouseOver { get; private set; }



		public Brush HoverBrush => Brushes.GhostWhite;

		public string StrokeDash => "";
		public string ToolTip => GetToolTip();

		public override string ToString() => "line";


		public void ToggleLine()
		{

		}


		private void SetLine()
		{
			Point sp = new Point(
				source.ItemBounds.Location.X + source.ItemBounds.Width / 2,
				source.ItemBounds.Location.Y + source.ItemBounds.Height);

			Point tp = new Point(
				target.ItemBounds.Location.X + target.ItemBounds.Width / 2,
				target.ItemBounds.Y);

			// Calculate the line boundaries (x, y, width, height)
			x = Math.Min(sp.X, tp.X);
			y = Math.Min(sp.Y, tp.Y);
			width = Math.Abs(tp.X - sp.X);
			height = Math.Abs(tp.Y - sp.Y);

			// The items bound has some margin around the line to allow full line width and arrow to show
			ItemBounds =
				new Rect(x - LineMargin, y - LineMargin, width + LineMargin * 2, height + LineMargin * 2);

			// Within the item bounds, the line will be drawn, and to do that we need some directions
			xf = sp.X < tp.X ? 1 : -1;
			yf = sp.Y < tp.Y ? 1 : -1;
			xd1 = sp.X < tp.X ? 0 : 1;
			yd1 = sp.Y < tp.Y ? 0 : 1;
			xd2 = sp.X < tp.X ? 1 : 0;
			yd2 = sp.Y < tp.Y ? 1 : 0;

			// The direction of the arrow head depend on the direction of the line
			arrowXd = (xf * width / (width + height)).MM(-0.9, 0.9);
			arrowYd = (yf * height / (height + width)).MM(-0.9, 0.9);
		}


		private void TrackSourceOrTargetChanges()
		{
			PropertyChangedEventManager.AddHandler(
				source, SourceOrTargetChanged, nameof(source.ItemBounds));
			PropertyChangedEventManager.AddHandler(
				target, SourceOrTargetChanged, nameof(source.ItemBounds));
		}


		private void SourceOrTargetChanged(object sender, PropertyChangedEventArgs e)
		{
			SetLine();
			NotifyAll();
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