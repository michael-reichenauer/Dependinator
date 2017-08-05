using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils;
using Dependinator.Utils.UI;

namespace Dependinator.ModelViewing.Links
{
	internal class LineViewModel : ItemViewModel
	{
		private readonly ILineViewModelService lineViewModelService;
		private readonly Line line;
		private readonly NodeViewModel source;
		private readonly NodeViewModel target;
		Point sourceAdjust = new Point(-1, -1);
		Point targetAdjust = new Point(-1, -1);

		private static readonly double LineMargin = 10;
		private readonly DelayDispatcher mouseOverDelay = new DelayDispatcher();

		
		private readonly List<Point> points = new List<Point> { new Point(0, 0), new Point(0, 0) };
		private int movingPointIndex = -1;


		public LineViewModel(ILineViewModelService lineViewModelService, Line line)
		{
			this.lineViewModelService = lineViewModelService;
			this.line = line;
			source = line.Source.ViewModel;
			target = line.Target.ViewModel;
			ItemZIndex = -1;

			UpdateLine();



			TrackSourceOrTargetChanges();
		}

		public override bool CanShow => source.CanShow & target.CanShow;



		public double LineWidth => GetLineLineWidth();

		public double ArrowWidth => GetArrowWidth();



		public Brush LineBrush => source.RectangleBrush;

		public bool IsMouseOver { get => Get(); private set => Set(value); }

		public string LineData => GetLineData();

		public string PointsData => GetPointsData();

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


		private string GetPointsData()
		{
			string lineData = "";
			double d = LineWidth.MM(0.5, 4);

			for (int i = 0; i < points.Count; i++)
			{
				Point m = LinePoint(points[i]);
				
				lineData += Txt.I($" M {m.X - d},{m.Y - d} H {m.X + d} V {m.Y + d} H {m.X - d} V {m.Y - d} H {m.X + d} ");
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
			Point p = ItemOwnerCanvas.RootScreenToCanvasPoint(point);

			//double min = double.MaxValue;
			int index = -1;

			for (int i = 0; i < points.Count - 1; i++)
			{
				Point a = points[i];
				Point b = points[i + 1];

				double aB = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
				double aP = Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
				double pb = Math.Sqrt((b.X - p.X) * (b.X - p.X) + (b.Y - p.Y) * (b.Y - p.Y));

				if (Math.Abs(aP) * ItemScale < 10)
				{
					if (i != 0)
					{
						movingPointIndex = i;
						return;
					}
				}
				else if (Math.Abs(pb) * ItemScale < 10)
				{
					// Prevent moving the target node endpoint
					if (i + 1 != points.Count - 1)
					{
						movingPointIndex = i + 1;
						return;
					}
				}

				double length = Math.Abs(aB - (aP + pb)) * ItemScale;
				if (length < 5)
				{
					index = i;
					//	min = length;
					break;
				}
			}

			IsMouseOver = true;

			// The point p is on the line between point a and b
			movingPointIndex = index + 1;
			points.Insert(movingPointIndex, p);
		}


		public void EndMoveLinePoint(Point point)
		{
			if (movingPointIndex == -1 || movingPointIndex >= points.Count)
			{
				return;
			}

			if (movingPointIndex != 0 && movingPointIndex != points.Count - 1)
			{
				Point p = points[movingPointIndex];

				Point a = points[movingPointIndex - 1];
				Point b = points[movingPointIndex + 1];

				double aB = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
				double aP = Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
				double pb = Math.Sqrt((b.X - p.X) * (b.X - p.X) + (b.Y - p.Y) * (b.Y - p.Y));

				double length = Math.Abs(aB - (aP + pb));
				if (length < 0.1)
				{
					points.RemoveAt(movingPointIndex);
				}
			}

			UpdateBounds();
			NotifyAll();

			movingPointIndex = -1;
		}


		public void MoveLinePoint(Point screenPoint)
		{
			if (movingPointIndex == -1 || movingPointIndex >= points.Count)
			{
				return;
			}


			Point point = ItemOwnerCanvas.RootScreenToCanvasPoint(screenPoint);
			if (movingPointIndex == 0)
			{
				point = GetPointInPerimeter(source.ItemBounds, point);
				sourceAdjust = new Point(
					(point.X - source.ItemBounds.X) / source.ItemBounds.Width,
					(point.Y - source.ItemBounds.Y) / source.ItemBounds.Height);

			}
			else if (movingPointIndex == points.Count - 1)
			{
				point = GetPointInPerimeter(target.ItemBounds, point);
				targetAdjust = new Point(
					(point.X - target.ItemBounds.X) / target.ItemBounds.Width,
					(point.Y - target.ItemBounds.Y) / target.ItemBounds.Height);
			}

			points[movingPointIndex] = point;

			UpdateBounds();
			IsMouseOver = true;
			NotifyAll();
		}

		
		private Point GetPointInPerimeter(Rect rect, Point point)
		{
			double r = rect.X + rect.Width;
			double b = rect.Y + rect.Height;

			double x = point.X.MM(rect.X, r);
			double y = point.Y.MM(rect.Y, b);
			
			double dl = Math.Abs(x - rect.X);
			double dr = Math.Abs(x - r);
			double dt= Math.Abs(y - rect.Y);
			double db = Math.Abs(y - b);

			double m = Math.Min(Math.Min(Math.Min(dl, dr), dt), db);

			if (Math.Abs(m - dt) < 0.01) return new Point(x, rect.Y);

			if (Math.Abs(m - db) < 0.01) return new Point(x, b);

			if (Math.Abs(m - dl) < 0.01) return new Point(rect.X, y);

			return new Point(r, y);
		}

	

		private void UpdateLine()
		{
			if (!CanShow)
			{
				return;
			}

			(Point sp, Point tp) = lineViewModelService.GetLineEndPoints(
				source.Node, target.Node, sourceAdjust, targetAdjust);

			points[0] = sp;
			points[points.Count - 1] = tp;

			UpdateBounds();
		}


		private void UpdateBounds()
		{
			Point sp = points[0];
			Point tp = points[points.Count - 1];

			// Calculate the line boundaries bases on first an last point
			double x = Math.Min(sp.X, tp.X);
			double y = Math.Min(sp.Y, tp.Y);
			double width = Math.Abs(tp.X - sp.X);
			double height = Math.Abs(tp.Y - sp.Y);

			Rect bounds = new Rect(x, y, width, height);

			// Adjust boundaries for line points between first and last
			for (int i = 1; i < points.Count - 1; i++)
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