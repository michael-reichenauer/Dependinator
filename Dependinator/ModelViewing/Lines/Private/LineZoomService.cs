using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.ModelHandling.Private;


namespace Dependinator.ModelViewing.Lines.Private
{
	internal class LineZoomService : ILineZoomService
	{
		public void ZoomInLinkLine(Line line)
		{
			IReadOnlyList<Link> links = line.Links.ToList();

			foreach (Link link in links)
			{
				IReadOnlyList<LinkSegment> currentLinkSegments = link.LinkSegments.ToList();

				var zoomedSegments = GetZoomedInBeforeReplacedSegments(currentLinkSegments, line.Source, line.Target);

				LinkSegment zoomedInSegment = GetZoomedInSegment(zoomedSegments, link);

				var newSegments = GetNewLinkSegments(currentLinkSegments, zoomedInSegment);

				var replacedLines = GetLines(link.Lines, zoomedSegments);

				//replacedLines.ForEach(replacedLine => HideLinkFromLine(replacedLine, link));

				//AddDirectLine(zoomedInSegment);

				link.LinkSegments = newSegments;
			}

			//line.Owner.RootNode.UpdateNodeVisibility();
		}



		private IReadOnlyList<LinkSegment> GetZoomedInBeforeReplacedSegments(
			IEnumerable<LinkSegment> linkSegments,
			Node source,
			Node target)
		{
			// Get the segments that are one before the line
			return linkSegments
				.SkipWhile(segment => segment.Source != source && segment.Target != source)
				.TakeWhile(segment => segment.Target == source || segment.Source == source)
				.ToList();
		}


		public LinkSegment GetZoomedInSegment(
			IReadOnlyList<LinkSegment> replacedSegments, Link link)
		{
			Node source = replacedSegments.First().Source;
			Node target = replacedSegments.Last().Target;

			//Node segmentOwner = source.AncestorsAndSelf()
			//	.First(node => target.AncestorsAndSelf().Contains(node));

			return new LinkSegment(source, target, link);
		}


		public IReadOnlyList<LinkSegment> GetNewLinkSegments(
			IReadOnlyList<LinkSegment> linkSegments, LinkSegment newSegment)
		{
			// Get the segments that are before the new segment
			IEnumerable<LinkSegment> preSegments = linkSegments
				.TakeWhile(segment => segment.Source != newSegment.Source);

			// Get the segments that are after the new segments
			IEnumerable<LinkSegment> postSegments = linkSegments
				.SkipWhile(segment => segment.Source != newSegment.Target);

			return
				preSegments
					.Concat(new[] { newSegment })
					.Concat(postSegments)
					.ToList();
		}


		private static IReadOnlyList<Line> GetLines(
			IReadOnlyList<Line> linkLines,
			IReadOnlyList<LinkSegment> replacedSegments)
		{
			return replacedSegments
				.Where(segment => linkLines.Any(
					line => line.Source == segment.Source && line.Target == segment.Target))
				.Select(segment => linkLines.First(
					line => line.Source == segment.Source && line.Target == segment.Target))
				.ToList();
		}


		//private void HideLinkFromLine(Line line, Link link)
		//{
		//	line.HideLink(link);
		//	link.Remove(line);

		//	if (//!line.IsMouseOver && 
		//	    !line.IsNormal && !line.Links.Any())
		//	{
		//		CloseLine(line);
		//	}
		//}



		//private bool AddDirectLine(LinkSegment segment)
		//{
		//	Node segmentOwner = segment.Source.AncestorsAndSelf()
		//		.First(node => segment.Target.AncestorsAndSelf().Contains(node));

		//	bool isNewAdded = false;
		//	Line existingLine = segmentOwner.SourceLines
		//		.FirstOrDefault(line => line.Source == segment.Source && line.Target == segment.Target);

		//	if (existingLine == null)
		//	{
		//		existingLine = new LinkLineOld(this, segment.Source, segment.Target, segment.Owner);
		//		AddLinkLine(existingLine);

		//		segment.Owner.AddOwnedLineItem(existingLine);

		//		segment.Source.AncestorsAndSelf()
		//			.TakeWhile(node => node != segment.Owner)
		//			.ForEach(node => node.Links.TryAddReferencedLine(existingLine));
		//		segment.Target.AncestorsAndSelf()
		//			.TakeWhile(node => node != segment.Owner)
		//			.ForEach(node => node.Links.TryAddReferencedLine(existingLine));
		//		isNewAdded = true;
		//	}

		//	existingLine.TryAddLink(segment.Link);
		//	segment.Link.TryAddLinkLine(existingLine);

		//	return isNewAdded;
		//}



		//public void CloseLine(Line line)
		//{
		//	line.Owner.RemoveOwnedLineItem(line);
		//	line.Owner.Links.RemoveOwnedLine(line);

		//	line.Source.Links.RemoveReferencedLine(line);
		//	line.Target.Links.RemoveReferencedLine(line);

		//	line.Source.AncestorsAndSelf()
		//		.TakeWhile(node => node != line.Owner)
		//		.ForEach(node => node.Links.RemoveReferencedLine(line));

		//	line.Target.AncestorsAndSelf()
		//		.TakeWhile(node => node != line.Owner)
		//		.ForEach(node => node.Links.RemoveReferencedLine(line));
		//}



		//public IReadOnlyList<LinkSegment> GetNewLinkSegments(
		//	IReadOnlyList<LinkSegment> linkSegments,
		//	IReadOnlyList<LinkSegment> newSegments)
		//{
		//	Node source = newSegments.First().Source;
		//	Node target = newSegments.Last().Target;

		//	// Get the segments that are before the new segment
		//	IEnumerable<LinkSegment> preSegments = linkSegments
		//		.TakeWhile(segment => segment.Source != source);

		//	// Get the segments that are after the new segments
		//	IEnumerable<LinkSegment> postSegments = linkSegments
		//		.SkipWhile(segment => segment.Source != target);

		//	return
		//		preSegments
		//			.Concat(newSegments)
		//			.Concat(postSegments)
		//			.ToList();
		//}


		//private IReadOnlyList<LinkSegment> GetZoomedInReplacedSegments(
		//	IEnumerable<LinkSegment> linkSegments,
		//	Node source,
		//	Node target)
		//{
		//	// Get the segments that are one before the line and one after the line
		//	return linkSegments
		//		.SkipWhile(segment => segment.Source != source && segment.Target != source)
		//		.TakeWhile(segment =>
		//			segment.Target == source || segment.Source == source || segment.Source == target)
		//		.ToList();
		//}



		//public void ZoomInLinkLine(LinkLineOld line, NodeOld node)
		//{
		//	//	IReadOnlyList<LinkOld> links = line.Links.ToList();

		//	//	foreach (LinkOld link in links)
		//	//	{
		//	//		IReadOnlyList<LinkSegmentOld> currentLinkSegments = link.LinkSegments.ToList();

		//	//		IReadOnlyList<LinkSegmentOld> zoomedSegments;
		//	//		if (node == line.Target)
		//	//		{
		//	//			zoomedSegments = segmentService.GetZoomedInBeforeReplacedSegments(currentLinkSegments, line.Source, line.Target);
		//	//		}
		//	//		else
		//	//		{
		//	//			zoomedSegments = segmentService.GetZoomedInAfterReplacedSegments(currentLinkSegments, line.Source, line.Target);
		//	//		}

		//	//		LinkSegmentOld zoomedInSegment = segmentService.GetZoomedInSegment(zoomedSegments, link);

		//	//		var newSegments = segmentService.GetNewLinkSegments(currentLinkSegments, zoomedInSegment);

		//	//		var replacedLines = GetLines(link.Lines, zoomedSegments);

		//	//		replacedLines.ForEach(replacedLine => HideLinkFromLine(replacedLine, link));

		//	//		link.SetLinkSegments(newSegments);
		//	//		if (AddDirectLine(zoomedInSegment))
		//	//		{
		//	//			break;
		//	//		}
		//	//	}

		//	//	line.Owner.RootNode.UpdateNodeVisibility();
		//	//}


		//public void ZoomOutLinkLine(LinkLine line)
		//{ 
		//	IReadOnlyList<Link> links = line.HiddenLinks.ToList();

		//	foreach (Link link in links)
		//	{
		//		IReadOnlyList<LinkSegment> normalLinkSegments = segmentService.GetNormalLinkSegments(link);
		//		IReadOnlyList<LinkSegment> currentLinkSegments = link.LinkSegments.ToList();

		//		var zoomedSegments = segmentService.GetZoomedOutReplacedSegments(normalLinkSegments, currentLinkSegments, line.Source, line.Target);

		//		LinkSegment zoomedInSegment = segmentService.GetZoomedInSegment(zoomedSegments, link);
		//		var replacedLines = GetLines(link.Lines, new [] { zoomedInSegment });
		//		replacedLines.ForEach(replacedLine => HideLinkFromLine(replacedLine, link));

		//		zoomedSegments.ForEach(segment => AddDirectLine(segment));

		//		var newSegments = segmentService.GetNewLinkSegments(currentLinkSegments, zoomedSegments);
		//		link.SetLinkSegments(newSegments);
		//	}

		//	line.Owner.AncestorsAndSelf().Last().UpdateNodeVisibility();
		//}


		//public void ZoomOutLinkLine(LinkLineOld line, NodeOld node)
		//{
		//	//IReadOnlyList<LinkOld> links = line.Links.ToList();

		//	//foreach (LinkOld link in links)
		//	//{
		//	//	IReadOnlyList<LinkSegmentOld> normalLinkSegments = segmentService.GetNormalLinkSegments(link);
		//	//	IReadOnlyList<LinkSegmentOld> currentLinkSegments = link.LinkSegments.ToList();

		//	//	IReadOnlyList<LinkSegmentOld> zoomedSegments = segmentService.GetZoomedOutReplacedSegments(normalLinkSegments, currentLinkSegments, line.Source, line.Target);

		//	//	if (zoomedSegments.Count > 1)
		//	//	{
		//	//		if (node == line.Target)
		//	//		{
		//	//			zoomedSegments = zoomedSegments.Skip(1).ToList();
		//	//		}
		//	//		else
		//	//		{
		//	//			zoomedSegments = zoomedSegments.Take(zoomedSegments.Count - 1).ToList();
		//	//		}

		//	//		LinkSegmentOld zoomedInSegment = segmentService.GetZoomedInSegment(zoomedSegments, link);

		//	//		var newSegments = segmentService.GetNewLinkSegments(normalLinkSegments, zoomedInSegment);

		//	//		HideLinkFromLine(line, link);

		//	//		newSegments.ForEach(segment => AddDirectLine(segment));


		//	//		link.SetLinkSegments(newSegments);
		//	//	}		
		//	//}

		//	//line.Owner.RootNode.UpdateNodeVisibility();
		//}


		//public void CloseLine(LinkLineOld line)
		//{
		//	line.Owner.RemoveOwnedLineItem(line);
		//	line.Owner.Links.RemoveOwnedLine(line);

		//	line.Source.Links.RemoveReferencedLine(line);
		//	line.Target.Links.RemoveReferencedLine(line);

		//	line.Source.AncestorsAndSelf()
		//		.TakeWhile(node => node != line.Owner)
		//		.ForEach(node => node.Links.RemoveReferencedLine(line));

		//	line.Target.AncestorsAndSelf()
		//		.TakeWhile(node => node != line.Owner)
		//		.ForEach(node => node.Links.RemoveReferencedLine(line));
		//}







		//private void AddNormalLinkSegment(LinkSegmentOld segment)
		//{
		//	LinkLineOld existingLine = segment.Owner.Links.OwnedLines
		//		.FirstOrDefault(line => line.Source == segment.Source && line.Target == segment.Target);

		//	if (existingLine == null)
		//	{
		//		existingLine = new LinkLineOld(this, segment.Source, segment.Target, segment.Owner);
		//		AddLinkLine(existingLine);
		//	}

		//	existingLine.IsNormal = true;

		//	existingLine.AddLink(segment.Link);
		//	segment.Link.TryAddLinkLine(existingLine);
		//}


		//private static void AddLinkLine(LinkLineOld line)
		//{
		//	line.Owner.Links.TryAddOwnedLine(line);
		//	line.Source.Links.TryAddReferencedLine(line);
		//	line.Target.Links.TryAddReferencedLine(line);
		//}


		//public double GetLineThickness(LinkLineOld linkLine)
		//{
		//	double scale = (linkLine.Owner.ItemsScale).MM(0.1, 0.7);
		//	double thickness;

		//	int linksCount = linkLine.Links.Count + linkLine.HiddenLinks.Count;

		//	if (linksCount < 5)
		//	{
		//		thickness = 1;
		//	}
		//	else if (linksCount < 15)
		//	{
		//		thickness = 2;
		//	}
		//	else
		//	{
		//		thickness = 3;
		//	}

		//	return thickness * scale;
		//}

		//private static Point DescendentPointToAncestorPoint(NodeOld descendent, NodeOld ancestor, Point point)
		//{
		//	foreach (NodeOld node in descendent.Ancestors())
		//	{
		//		if (node == ancestor)
		//		{
		//			break;
		//		}

		//		point = node.ChildCanvasPointToParentCanvasPoint(point);
		//	}

		//	return point;
		//}


		//private static Point ParentPointToChildPoint(NodeOld parent, Point point)
		//{
		//	return parent.ParentCanvasPointToChildCanvasPoint(point);
		//}


		//private static bool IsNodesInitialized(LinkLineOld line) =>
		//	line.Source.ItemBounds != Rect.Empty && line.Target.ItemBounds != Rect.Empty;



		///// <summary>
		///// Gets the links in the line grouped first by source and then by target at the
		///// appropriate node levels.
		///// </summary>
		//public IReadOnlyList<LinkGroup> GetLinkGroups2(Line line)
		//{
		//	Node source = line.Source;
		//	Node target = line.Target;
		//	IReadOnlyList<Link> links = line.Links;

		//	List<LinkGroup> linkGroups = new List<LinkGroup>();

		//	var groupByTargets = links.GroupBy(link => link.Target);
		//	foreach (IGrouping<Node, Link> groupByTarget in groupByTargets)
		//	{
		//		var groupBySourceParents = groupByTarget.GroupBy(link => link.Source.Parent);

		//		foreach (var groupBySourceParent in groupBySourceParents)
		//		{
		//			LinkGroup linkGroup = new LinkGroup(
		//				groupBySourceParent.Key, groupByTarget.Key, groupBySourceParent.ToList());

		//			linkGroups.Add(linkGroup);
		//		}
		//	}

		//	return linkGroups;
		//}


		///// <summary>
		///// Gets the links in the line grouped first by source and then by target at the
		///// appropriate node levels.
		///// </summary>
		//public IReadOnlyList<LinkGroup> GetLinkGroups(Line line)
		//{
		//	Node source = line.Source;
		//	Node target = line.Target;
		//	IReadOnlyList<Link> links = line.Links;

		//	(int sourceLevel, int targetLevel) = GetNodeLevels(source, target);

		//	List<LinkGroup> linkGroups = new List<LinkGroup>();

		//	// RootGroup links by grouping them based on node at source level
		//	var groupBySources = links.GroupBy(link => NodeAtLevel(link.Source, sourceLevel));
		//	foreach (var groupBySource in groupBySources)
		//	{
		//		// Sub-group these links by grouping them based on node at target level
		//		var groupByTargets = groupBySource.GroupBy(link => NodeAtLevel(link.Target, targetLevel));
		//		foreach (var groupByTarget in groupByTargets)
		//		{
		//			Node sourceNode = groupBySource.Key;
		//			Node targetNode = groupByTarget.Key;
		//			List<Link> groupLinks = groupByTarget.ToList();

		//			LinkGroup linkGroup = new LinkGroup(sourceNode, targetNode, groupLinks);
		//			linkGroups.Add(linkGroup);
		//		}
		//	}

		//	return linkGroups;
		//}


		//private static (int sourceLevel, int targetLevel) GetNodeLevels(Node source, Node target)
		//{
		//	int sourceLevel = source.Ancestors().Count();
		//	int targetLevel = target.Ancestors().Count();

		//	if (source == target.Parent)
		//	{
		//		// Source node is parent of target
		//		targetLevel += 1;
		//	}
		//	//else if (source.Parent == target)
		//	//{
		//	//	// Source is child of target
		//	//	// sourceLevel += 1;
		//	//}
		//	//else
		//	//{
		//	//	// Siblings, dig into both source and level
		//	//	//sourceLevel += 1;
		//	//	//targetLevel += 1;
		//	//}

		//	return (sourceLevel, targetLevel);
		//}


		//private static Node NodeAtLevel(Node node, int level)
		//{
		//	int count = 0;
		//	Node current = null;
		//	foreach (Node ancestor in node.AncestorsAndSelf().Reverse())
		//	{
		//		current = ancestor;
		//		if (count++ == level)
		//		{
		//			break;
		//		}
		//	}

		//	return current;
		//}
	}
}