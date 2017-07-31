using System;
using System.Collections.Specialized;
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


		private Point sp;
		private Point tp;

		public LineViewModel(
			ILineViewModelService lineViewModelService,
			Line line)
		{
			this.lineViewModelService = lineViewModelService;
			this.line = line;
			source = line.Source.ViewModel;
			target = line.Target.ViewModel;
			ItemZIndex = -1;

			SetLine();
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
			Point s = LinePoint(sp);
			Point t = LinePoint(tp);
			
			return Txt.I($"M {s.X},{s.Y} L {s.X},{s.Y + 5} L {t.X},{t.Y-10} L {t.X},{t.Y - 6}");
		}

		private string GetArrowData()
		{
			Point t = LinePoint(tp);

			return Txt.I($"M {t.X},{t.Y-6.5} L {t.X},{t.Y -4.5}");
		}

		private Point LinePoint(Point p) => 
			new Point((p.X - ItemBounds.X)*ItemScale, (p.Y - ItemBounds.Y) * ItemScale);


		public string StrokeDash => "";
		public string ToolTip => GetToolTip();

		public override string ToString() => $"{line}";


		public void ToggleLine()
		{

		}


		private void SetLine()
		{
			if (!CanShow)
			{
				return;
			}

			(sp, tp) = lineViewModelService.GetLineEndPoints(source.Node, target.Node);

			// Calculate the line boundaries (x, y, width, height)
			double x = Math.Min(sp.X, tp.X);
			double y = Math.Min(sp.Y, tp.Y);
			double width = Math.Abs(tp.X - sp.X);
			double height = Math.Abs(tp.Y - sp.Y);

			// The items bound has some margin around the line to allow full line width and arrow to show
			ItemBounds = new Rect(
				x - LineMargin / ItemScale, 
				y - (LineMargin) / ItemScale, 
				width + (LineMargin * 2) / ItemScale,
				height + (LineMargin * 2) / ItemScale);
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