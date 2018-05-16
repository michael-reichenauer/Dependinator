using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Dependinator.Common;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.ReferencesViewing;


namespace Dependinator.ModelViewing.Lines.Private
{
	internal class LineViewModelService : ILineViewModelService
	{
		private readonly ILineMenuItemService lineMenuItemService;
		private readonly ILineControlService lineControlService;
		private readonly ILineZoomService lineZoomService;
		private readonly ILineDataService lineDataService;
		private readonly IItemSelectionService itemSelectionService;
		private readonly IReferenceItemService referenceItemService;

		private readonly WindowOwner owner;


		public LineViewModelService(
			ILineMenuItemService lineMenuItemService,
			ILineControlService lineControlService,
			ILineZoomService lineZoomService,
			ILineDataService lineDataService,
			IItemSelectionService itemSelectionService,
			IReferenceItemService referenceItemService,
			WindowOwner owner)
		{
			this.lineMenuItemService = lineMenuItemService;
			this.lineControlService = lineControlService;
			this.lineZoomService = lineZoomService;
			this.lineDataService = lineDataService;
			this.itemSelectionService = itemSelectionService;
			this.referenceItemService = referenceItemService;
			this.owner = owner;
		}


		public void UpdateLineBounds(Line line) => lineDataService.UpdateLineBounds(line);

		public double GetLineWidth(Line line) => lineDataService.GetLineWidth(line);


		public string GetLineData(Line line) => lineDataService.GetLineData(line);

		public string GetPointsData(Line line) => lineDataService.GetPointsData(line);


		public string GetArrowData(Line line) => lineDataService.GetArrowData(line);


		public double GetArrowWidth(Line line) => lineDataService.GetArrowWidth(line);


		public string GetEndPointsData(Line line) => lineDataService.GetEndPointsData(line);


		public void UpdateLineEndPoints(Line line) => lineDataService.UpdateLineEndPoints(line);


		public LineControl GetLineControl(Line line) => new LineControl(lineControlService, line);


		public IEnumerable<LineMenuItemViewModel> GetTargetLinkItems(Line line) =>
			lineMenuItemService.GetTargetLinkItems(line);


		public IEnumerable<LineMenuItemViewModel> GetSourceLinkItems(Line line) =>
			lineMenuItemService.GetSourceLinkItems(line);


		public void Clicked(LineViewModel lineViewModel) => itemSelectionService.Select(lineViewModel);


		public void OnMouseWheel(LineViewModel lineViewModel, UIElement uiElement, MouseWheelEventArgs e)
		{
			if (lineViewModel.Line.Owner.View.ViewModel != null)
			{
				lineViewModel.Line.Owner.View.ViewModel.OnMouseWheel(uiElement, e);
			}
			else
			{
				lineViewModel.Line.Owner.Root.View.ItemsCanvas.OnMouseWheel(uiElement, e, false);
			}
		}


		public void Toggle(Line line)
		{
			lineZoomService.ZoomInLinkLine(line);
		}


		public void ShowReferences(LineViewModel lineViewModel, bool isIncoming)
		{
			Line line = lineViewModel.Line;
			var referenceItems = referenceItemService.GetReferences(line, new ReferenceOptions(isIncoming));

			Node node = isIncoming ? line.Target : line.Source;
			ReferencesDialog referencesDialog = new ReferencesDialog(
				owner, node, referenceItems, isIncoming);
			referencesDialog.Show();

		}


		//public void AddLinkLines(LinkOld link)
		//{
		//	//var linkSegments = segmentService.GetNormalLinkSegments(link);

		//	//linkSegments.ForEach(AddNormalLinkSegment);
		//	//link.SetLinkSegments(linkSegments);
		//}









		//public LinkLineBounds GetLinkLineBounds(LinkLineOld line)
		//{
		//	if (!IsNodesInitialized(line))
		//	{
		//		return LinkLineBounds.Empty;
		//	}

		//	(Point p1, Point p2) = GetLinkSegmentEndPoints(line);

		//	// Ensure the rect is at least big enough to contain the width of the actual line
		//	double margin = 2.5 / line.ItemsScale;

		//	Rect lineBounds = GetLineBounds(p1, p2, margin);

		//	(Point l1, Point l2) = GetLineEndPoints(p1, p2, margin);

		//	return new LinkLineBounds(lineBounds, l1, l2);
		//}


		//private static (Point l1, Point l2) GetLineEndPoints(Point p1, Point p2, double margin)
		//{
		//	// Line drawing within the bounds
		//	double width = Math.Abs(p2.X - p1.X);
		//	double height = Math.Abs(p2.Y - p1.Y);


		//	if (p1.X <= p2.X && p1.Y <= p2.Y)
		//	{
		//		return (new Point(margin, margin), new Point(width, height));
		//	}
		//	else if (p1.X > p2.X && p1.Y <= p2.Y)
		//	{
		//		return (new Point(width, margin), new Point(margin, height));
		//	}
		//	else if (p1.X <= p2.X && p1.Y > p2.Y)
		//	{
		//		return (new Point(margin, height), new Point(width, margin));
		//	}
		//	else
		//	{
		//		return (new Point(width, height), new Point(margin, margin));
		//	}
		//}




		//private static Rect GetLineBounds(Point p1, Point p2, double margin)
		//{
		//	// Line bounds:
		//	double x = Math.Min(p1.X, p2.X);
		//	double y = Math.Min(p1.Y, p2.Y);
		//	double width = Math.Abs(p2.X - p1.X);
		//	double height = Math.Abs(p2.Y - p1.Y);

		//	x = x - margin;
		//	y = y - margin;
		//	width = width + margin * 2;
		//	height = height + margin * 2;

		//	return new Rect(x, y, width, height);
		//}


		//private static (Point source, Point target) GetLinkSegmentEndPoints(LinkLineOld line)
		//{
		//	NodeOld source = line.Source;
		//	NodeOld target = line.Target;
		//	Rect sourceBounds = source.ItemBounds;
		//	Rect targetBounds = target.ItemBounds;


		//	if (source.ParentNode == target.ParentNode)
		//	{
		//		// Source and target nodes are siblings, 
		//		// ie. line starts at source middle bottom and ends at target middle top
		//		double x1 = sourceBounds.X + sourceBounds.Width / 2;
		//		double y1 = sourceBounds.Y + sourceBounds.Height;
		//		Point sp = new Point(x1, y1);

		//		double x2 = targetBounds.X + targetBounds.Width / 2;
		//		double y2 = targetBounds.Y;
		//		Point tp = new Point(x2, y2);

		//		return (sp, tp);
		//	}
		//	else if (source.ParentNode == target)
		//	{
		//		// The target is a parent of the source,
		//		// i.e. line ends at the bottom of the target node
		//		double x1 = sourceBounds.X + sourceBounds.Width / 2;
		//		double y1 = sourceBounds.Y + sourceBounds.Height;
		//		Point sp = new Point(x1, y1);

		//		double x2 = targetBounds.X + targetBounds.Width / 2;
		//		double y2 = targetBounds.Y + targetBounds.Height;
		//		Point tp = ParentPointToChildPoint(target, new Point(x2, y2));

		//		return (sp, tp);
		//	}
		//	else if (source == target.ParentNode)
		//	{
		//		// The target is the child of the source,
		//		// i.e. line start at the top of the source
		//		double x1 = sourceBounds.X + sourceBounds.Width / 2;
		//		double y1 = sourceBounds.Y;
		//		Point sp = ParentPointToChildPoint(source, new Point(x1, y1));

		//		double x2 = targetBounds.X + targetBounds.Width / 2;
		//		double y2 = targetBounds.Y;
		//		Point tp = new Point(x2, y2);

		//		return (sp, tp);
		//	}
		//	else
		//	{
		//		// The line is between nodes, which are not within same node
		//		if (source == line.Owner)
		//		{
		//			double x1 = sourceBounds.X + sourceBounds.Width / 2;
		//			double y1 = sourceBounds.Y;
		//			Point sp = ParentPointToChildPoint(source, new Point(x1, y1));

		//			double x2 = targetBounds.X + targetBounds.Width / 2;
		//			double y2 = targetBounds.Y;
		//			Point tp = DescendentPointToAncestorPoint(target, line.Owner, new Point(x2, y2));

		//			return (sp, tp);
		//		}
		//		else if (target == line.Owner)
		//		{
		//			double x1 = sourceBounds.X + sourceBounds.Width / 2;
		//			double y1 = sourceBounds.Y + sourceBounds.Height;
		//			Point sp = DescendentPointToAncestorPoint(target, line.Owner, new Point(x1, y1));

		//			double x2 = targetBounds.X + targetBounds.Width / 2;
		//			double y2 = targetBounds.Y + targetBounds.Height;
		//			Point tp = ParentPointToChildPoint(target, new Point(x2, y2));

		//			return (sp, tp);
		//		}
		//		else
		//		{
		//			// Nodes are not direct siblings, need to use the common ancestor (owner)
		//			double x1 = sourceBounds.X + sourceBounds.Width / 2;
		//			double y1 = sourceBounds.Y + sourceBounds.Height;
		//			Point sp = DescendentPointToAncestorPoint(source, line.Owner, new Point(x1, y1));

		//			double x2 = targetBounds.X + targetBounds.Width / 2;
		//			double y2 = targetBounds.Y;
		//			Point tp = DescendentPointToAncestorPoint(target, line.Owner, new Point(x2, y2));

		//			return (sp, tp);
		//		}
		//	}
		//}



	}
}