using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils.UI;

namespace Dependinator.ModelViewing.Links
{
	internal class LineViewModel : ItemViewModel
	{
		private readonly ILineViewModelService lineViewModelService;
		private readonly Line line;
		private readonly Node owner;
		private readonly NodeViewModel source;
		private readonly NodeViewModel target;
		//private static readonly double NodeMargin = 0.8 * 2;
		//private static readonly double ArrowLength = 1.0;
		private static readonly double LineMargin = 10;
		private readonly DelayDispatcher mouseOverDelay = new DelayDispatcher();



		// Line and arrow variables for boundary, position and direction
		//private double x;
		//private double y;
		//private double width;
		//private double height;
		//private int xf;
		//private int yf;
		//private int xd1;
		//private int yd1;
		//private int xd2;
		//private int yd2;
		//private double arrowXd;
		//private double arrowYd;



		private readonly List<Point> points = new List<Point> { new Point(0, 0), new Point(0, 0) };
		private int movingPointIndex = -1;

		public LineViewModel(ILineViewModelService lineViewModelService, Line line, Node owner)
		{
			this.lineViewModelService = lineViewModelService;
			this.line = line;
			this.owner = owner;
			source = line.Source.ViewModel;
			target = line.Target.ViewModel;
			ItemZIndex = -1;

			UpdateLine();



			TrackSourceOrTargetChanges();
		}

		public override bool CanShow => source.CanShow & target.CanShow;


		//// The line endpoints (x1,y1)->(x2,y2) within the item bounds with margins and direction
		//public double X1 => (width * xd1 + LineMargin) * ItemScale - NodeMargin;
		//public double Y1 => (height * yd1 + LineMargin) * ItemScale - NodeMargin;
		//public double X2 => (width * xd2 + LineMargin) * ItemScale - NodeMargin;
		//public double Y2 => (height * yd2 + LineMargin) * ItemScale - NodeMargin * 2;


		public double LineWidth => GetLineLineWidth();

		//// The arrow line endpoint (based on the line end (x2,y2) and with size and direction
		//public double ArrowX1 => X2 - (ArrowWidth * arrowXd / 2) + (ArrowLength * 2 * arrowXd);
		//public double ArrowY1 => Y2 - (ArrowWidth * arrowYd / 2) + (ArrowLength * 2 * arrowYd);
		//public double ArrowX2 => X2 - (ArrowWidth * arrowXd / 2) + (ArrowLength * 3 * arrowXd);
		//public double ArrowY2 => Y2 - (ArrowWidth * arrowYd / 2) + (ArrowLength * 3 * arrowYd);
		//// The direction of the arrow head depend on the direction of the line
		//double arrowXd = (xf * width / (width + height)).MM(-0.9, 0.9);
		//double arrowYd = (yf * height / (height + width)).MM(-0.9, 0.9);

		public double ArrowWidth => GetArrowWidth();



		public Brush LineBrush => source.RectangleBrush;

		public bool IsMouseOver { get; private set; }

		public string LineData => GetLineData();

		public string ArrowData => GetArrowData();


		private string GetLineData()
		{
			Point s = LinePoint(points[0]);
			Point t = LinePoint(points[points.Count - 1]);

			return Txt.I($"M {s.X},{s.Y} {GetMiddleLineData()} L {t.X},{t.Y - 10} L {t.X},{t.Y - 6.5}");
		}


		private string GetMiddleLineData()
		{
			string lineData = "";

			for (int i = 1; i < points.Count - 1; i++)
			{
				Point m = LinePoint(points[i]);
				lineData += Txt.I($" L {m.X},{m.Y}");
			}

			return lineData;
		}


		private string GetArrowData()
		{
			Point t = LinePoint(points[points.Count - 1]);

			return Txt.I($"M {t.X},{t.Y - 6.5} L {t.X},{t.Y - 4.5}");
		}

		private Point LinePoint(Point p) =>
			new Point((p.X - ItemBounds.X) * ItemScale, (p.Y - ItemBounds.Y) * ItemScale);


		public string StrokeDash => "";
		public string ToolTip => GetToolTip();

		public override string ToString() => $"{line}";


		public void ToggleLine()
		{

		}




		public void BeginMoveLinePoint(Point point)
		{
			Point p = owner.ItemsCanvas.RootScreenToCanvasPoint(point);

			//double min = double.MaxValue;
			int index = -1;

			for (int i = 0; i < points.Count - 1; i++)
			{
				Point a = points[i];
				Point b = points[i + 1];

				double aB = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
				double aP = Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
				double pb = Math.Sqrt((b.X - p.X) * (b.X - p.X) + (b.Y - p.Y) * (b.Y - p.Y));

				if (Math.Abs(aP) < 5)
				{
					movingPointIndex = i;
					return;
				}
				else if (Math.Abs(pb) < 5)
				{
					movingPointIndex = i + 1;
					return;
				}

				double length = Math.Abs(aB - (aP + pb));
				if (length < 1)
				{
					index = i;
					//	min = length;
					break;
				}
			}

			// The point p is on the line between point a and b
			movingPointIndex = index + 1;
			points.Insert(movingPointIndex, p);
		}


		public void EndMoveLinePoint(Point point)
		{
			movingPointIndex = -1;
		}


		public void MoveLinePoint(Point point)
		{
			if (movingPointIndex == -1 || movingPointIndex >= points.Count)
			{
				return;
			}

			points[movingPointIndex] = owner.ItemsCanvas.RootScreenToCanvasPoint(point);

			UpdateBounds(points[0], points[points.Count - 1]);

			NotifyAll();
		}


		private void UpdateLine()
		{
			if (!CanShow)
			{
				return;
			}

			(Point sp, Point tp) = lineViewModelService.GetLineEndPoints(source.Node, target.Node);
			points[0] = sp;
			points[points.Count - 1] = tp;

			UpdateBounds(sp, tp);
		}


		private void UpdateBounds(Point sp, Point tp)
		{
			// Calculate the line boundaries (x, y, width, height)
			double x = Math.Min(sp.X, tp.X);
			double y = Math.Min(sp.Y, tp.Y);
			double width = Math.Abs(tp.X - sp.X);
			double height = Math.Abs(tp.Y - sp.Y);

			Rect bounds = new Rect(x, y, width, height);

			for (int i = 0; i < points.Count - 1; i++)
			{
				bounds.Union(points[i]);
			}

			// The items bound has some margin around the line to allow full line width and arrow to show
			ItemBounds = new Rect(
				bounds.X - LineMargin / ItemScale,
				bounds.Y - (LineMargin) / ItemScale,
				bounds.Width + (LineMargin * 2) / ItemScale,
				bounds.Height + (LineMargin * 2) / ItemScale);
		}


		private void TrackSourceOrTargetChanges()
		{
			WhenSet(source, nameof(source.ItemBounds))
				.Notify(SourceOrTargetChanged);
			WhenSet(target, nameof(target.ItemBounds))
				.Notify(SourceOrTargetChanged);

			// When canvas is moved lines anchored to parent top or bottom needs to be updated  
			if (source.Node == target.Node.Parent)
			{
				WhenSet(source.Node.ItemsCanvas, nameof(source.Node.ItemsCanvas.Offset))
					.Notify(SourceOrTargetChanged);
			}

			if (target.Node == source.Node.Parent)
			{
				WhenSet(target.Node.ItemsCanvas, nameof(target.Node.ItemsCanvas.Offset))
					.Notify(SourceOrTargetChanged);
			}
		}


		private double GetLineLineWidth()
		{
			double lineWidth;

			int linksCount = line.Links.Count;

			if (linksCount < 5)
			{
				lineWidth = 1;
			}
			else if (linksCount < 15)
			{
				lineWidth = 4;
			}
			else
			{
				lineWidth = 6;
			}

			double lineLineWidth = (lineWidth * 0.7 * ItemScale).MM(0.1, 4);

			if (IsMouseOver)
			{
				lineLineWidth = (lineLineWidth * 1.5).MM(0, 6);
			}

			return lineLineWidth;
		}


		private double GetArrowWidth()
		{
			double arrowWidth = (10 * ItemScale).MM(4, 15);

			if (IsMouseOver)
			{
				arrowWidth = (arrowWidth * 1.5).MM(0, 20);
			}

			return arrowWidth;
		}


		private void SourceOrTargetChanged(string propertyName)
		{
			UpdateLine();
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

			return $"{line} ({line.Links.Count} links)";
		}


		public void ZoomLinks(double zoom, Point viewPosition)
		{
		}


		public void OnMouseEnter()
		{
			mouseOverDelay.Delay(TimeSpan.FromMilliseconds(100), _ =>
			{
				IsMouseOver = true;
				Notify(nameof(LineBrush), nameof(LineWidth), nameof(ArrowWidth));
			});
		}


		public void OnMouseLeave()
		{
			mouseOverDelay.Cancel();
			IsMouseOver = false;
			Notify(nameof(LineBrush), nameof(LineWidth), nameof(ArrowWidth));
		}


	}
}