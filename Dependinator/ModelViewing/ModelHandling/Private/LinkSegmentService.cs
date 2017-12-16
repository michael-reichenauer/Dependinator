using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal class LinkSegmentService : ILinkSegmentService
	{
		public IReadOnlyList<LinkSegment> GetLinkSegments(Link link)
		{
			if (link.Source.Parent == link.Target.Parent ||
			    link.Source == link.Target.Parent ||
			    link.Source.Parent == link.Target)
			{
				// Sibling, parent or child link
				return new[] { new LinkSegment(link.Source, link.Target, link) };
			}

			List<LinkSegment> segments = new List<LinkSegment>();
			List<Node> sourceAncestors = SourceAncestorsAndSel(link).Reverse().ToList();
			List<Node> targetAncestors = TargetAncestorsAndSelf(link).Reverse().ToList();
			
			for (int i = 0; i < sourceAncestors.Count; i++)
			{
				if (sourceAncestors[i] != targetAncestors[i])
				{
					// Cousins (nodes within siblings)
					Node segmentSource = link.Source;

					// From source going upp to common ancestor
					for (int j = sourceAncestors.Count - 2; j >= i; j--)
					{
						Node segmentTarget = sourceAncestors[j];
						segments.Add(new LinkSegment(segmentSource, segmentTarget, link));
						segmentSource = segmentTarget;
					}

					// From common ancestor going down to target
					for (int j = i; j < targetAncestors.Count; j++)
					{
						Node segmentTarget = targetAncestors[j];
						segments.Add(new LinkSegment(segmentSource, segmentTarget, link));
						segmentSource = segmentTarget;
					}

					break;
				}
				else if (link.Source == targetAncestors[i])
				{
					// Source is a direct ancestor of th target
					Node segmentSource = link.Source;

					// From source going down to target
					for (int j = i + 1; j < targetAncestors.Count; j++)
					{
						Node segmentTarget = targetAncestors[j];
						segments.Add(new LinkSegment(segmentSource, segmentTarget, link));
						segmentSource = segmentTarget;
					}

					break;
				}
				else if (link.Target == sourceAncestors[i])
				{
					// Target is a direct ancestor of the source
					Node segmentSource = link.Source;

					// From source going upp to target
					for (int j = sourceAncestors.Count - 2; j >= i ; j--)
					{
						Node segmentTarget = sourceAncestors[j];
						segments.Add(new LinkSegment(segmentSource, segmentTarget, link));
						segmentSource = segmentTarget;
					}

					break;
				}
			}

			return segments;
		}


		//public IReadOnlyList<LinkSegment> GetLinkSegments(Link link)
		//{
		//	if (link.Source.Parent == link.Target.Parent)
		//	{
		//		return new[] { new LinkSegment(link.Source, link.Target, link) };
		//	}
		//	else if (link.Source == link.Target.Parent)
		//	{
		//		return new[] { new LinkSegment(link.Source, link.Target, link) };
		//	}
		//	else if (link.Source.Parent == link.Target)
		//	{
		//		return new[] { new LinkSegment(link.Source, link.Target, link) };
		//	}


		//	List<LinkSegment> segments = new List<LinkSegment>();
		//	IEnumerable<Node> sourceAncestors = SourceAncestorsAndSel(link);
		//	List<Node> sourceAncestors2 = SourceAncestorsAndSel(link).Reverse().ToList();
		//	List<Node> targetAncestors = TargetAncestorsAndSelf(link).Reverse().ToList();



		//	// Start with first line at the start of the segmented line 
		//	Node segmentSource = link.Source;
		//	foreach (Node sourceAncestor in sourceAncestors)
		//	{
		//		for (int i = 0; i < targetAncestors.Count; i++)
		//		{
		//			Node targetAncestor = targetAncestors[i];
		//			if (segmentSource != link.Source)
		//			{
		//				if (sourceAncestor == targetAncestor)
		//				{
		//					for (int j = i + 1; j < targetAncestors.Count; j++)
		//					{
		//						targetAncestor = targetAncestors[j];
		//						segments.Add(new LinkSegment(segmentSource, targetAncestor, link));
		//						segmentSource = targetAncestor;
		//					}

		//					return segments;
		//				}
		//			}
		//			else
		//			{
		//				if (sourceAncestor == targetAncestors[i])
		//				{
		//					for (int j = i; j + 1 < targetAncestors.Count; j++)
		//					{
		//						targetAncestor = targetAncestors[j];
		//						segments.Add(new LinkSegment(segmentSource, targetAncestor, link));
		//						segmentSource = targetAncestor;
		//					}

		//					return segments;
		//				}
		//			}
		//		}

		//		if (segmentSource != sourceAncestor)
		//		{
		//			segments.Add(new LinkSegment(segmentSource, sourceAncestor, link));
		//		}

		//		segmentSource = sourceAncestor;
		//	}

		//	return segments;
		//}


		private static IEnumerable<Node> SourceAncestorsAndSel(Link link)
		{
			foreach (Node node in link.Source.AncestorsAndSelf())
			{
				yield return node;
			}

			yield return link.Source.Root;
		}


		private static IEnumerable<Node> TargetAncestorsAndSelf(Link link)
		{
			foreach (var node in link.Target.AncestorsAndSelf())
			{
				yield return node;
			}

			yield return link.Target.Root;
		}


		//	public IReadOnlyList<LinkSegment> GetLinkSegments(Link link)
		//{
		//	List<LinkSegment> segments = new List<LinkSegment>();

		//	// Start with first line at the start of the segmented line 
		//	Node segmentSource = link.Source;

		//	// Iterate segments until line end is reached
		//	while (segmentSource != link.Target)
		//	{
		//		// Start by trying if next line segment target is a sibling, child or descendent node by 
		//		// searching if line segment source is a ancestor of end target node
		//		Node segmentTarget = link.Target.AncestorsAndSelf()
		//			.FirstOrDefault(targetAncestor =>
		//				targetAncestor.Parent == segmentSource.Parent ||
		//				targetAncestor.Parent == segmentSource);

		//		if (segmentTarget == null)
		//		{
		//			// Segment target was neither sibling or child nor a child, next line target node must
		//			// be the parent node
		//			segmentTarget = segmentSource.Parent;
		//		}

		//		LinkSegment segment = new LinkSegment(segmentSource, segmentTarget, link);
		//		segments.Add(segment);

		//		// Go to next line in the line segments 
		//		segmentSource = segmentTarget;
		//	}

		//	return segments;
		//}




		//public IReadOnlyList<LinkSegmentOld> GetNewLinkSegments(
		//	IReadOnlyList<LinkSegmentOld> linkSegments, LinkSegmentOld newSegment)
		//{
		//	// Get the segments that are before the new segment
		//	IEnumerable<LinkSegmentOld> preSegments = linkSegments
		//		.TakeWhile(segment => segment.Source != newSegment.Source);

		//	// Get the segments that are after the new segments
		//	IEnumerable<LinkSegmentOld> postSegments = linkSegments
		//		.SkipWhile(segment => segment.Source != newSegment.Target);

		//	return
		//		preSegments
		//			.Concat(new[] { newSegment })
		//			.Concat(postSegments)
		//			.ToList();
		//}


		//public IReadOnlyList<LinkSegmentOld> GetNewLinkSegments(
		//	IReadOnlyList<LinkSegmentOld> linkSegments,
		//	IReadOnlyList<LinkSegmentOld> newSegments)
		//{
		//	NodeOld source = newSegments.First().Source;
		//	NodeOld target = newSegments.Last().Target;

		//	// Get the segments that are before the new segment
		//	IEnumerable<LinkSegmentOld> preSegments = linkSegments
		//		.TakeWhile(segment => segment.Source != source);

		//	// Get the segments that are after the new segments
		//	IEnumerable<LinkSegmentOld> postSegments = linkSegments
		//		.SkipWhile(segment => segment.Source != target);

		//	return
		//		preSegments
		//			.Concat(newSegments)
		//			.Concat(postSegments)
		//			.ToList();
		//}


		//public LinkSegmentOld GetZoomedInSegment(
		//	IReadOnlyList<LinkSegmentOld> replacedSegments, LinkOld link)
		//{
		//	NodeOld source = replacedSegments.First().Source;
		//	NodeOld target = replacedSegments.Last().Target;

		//	NodeOld segmentOwner = source.AncestorsAndSelf()
		//		.First(node => target.AncestorsAndSelf().Contains(node));

		//	return new LinkSegmentOld(source, target, segmentOwner, link);
		//}



		//public IReadOnlyList<LinkSegmentOld> GetZoomedInReplacedSegments(
		//	IEnumerable<LinkSegmentOld> linkSegments,
		//	NodeOld source,
		//	NodeOld target)
		//{
		//	// Get the segments that are one before the line and one after the line
		//	return linkSegments
		//		.SkipWhile(segment => segment.Source != source && segment.Target != source)
		//		.TakeWhile(segment =>
		//			segment.Target == source || segment.Source == source || segment.Source == target)
		//		.ToList();
		//}

		//public IReadOnlyList<LinkSegmentOld> GetZoomedInBeforeReplacedSegments(
		//	IEnumerable<LinkSegmentOld> linkSegments,
		//	NodeOld source,
		//	NodeOld target)
		//{
		//	// Get the segments that are one before the line
		//	return linkSegments
		//		.SkipWhile(segment => segment.Source != source && segment.Target != source)
		//		.TakeWhile(segment => segment.Target == source || segment.Source == source)
		//		.ToList();
		//}



		//public IReadOnlyList<LinkSegmentOld> GetZoomedInAfterReplacedSegments(
		//	IEnumerable<LinkSegmentOld> linkSegments,
		//	NodeOld source,
		//	NodeOld target)
		//{
		//	// Get the segments that are one after the line
		//	return linkSegments
		//		.SkipWhile(segment => segment.Source != source)
		//		.TakeWhile(segment => segment.Source == source || segment.Source == target)
		//		.ToList();
		//}


		//public IReadOnlyList<LinkSegmentOld> GetZoomedOutReplacedSegments(
		//	IReadOnlyList<LinkSegmentOld> normalSegments,
		//	IReadOnlyList<LinkSegmentOld> currentSegments,
		//	NodeOld source,
		//	NodeOld target)
		//{
		//	int index1 = normalSegments.TakeWhile(s => s.Source != source).Count();
		//	int a1 = 0;
		//	for (int i = index1; i > -1; i--)
		//	{
		//		LinkSegmentOld segment = normalSegments[i];
		//		if (currentSegments.Any(s => s.Source == segment.Source))
		//		{
		//			a1 = i;
		//			break;
		//		}
		//	}

		//	int index2 = normalSegments.TakeWhile(s => s.Target != target).Count();
		//	int a2 = 0;
		//	for (int i = index2; i < normalSegments.Count; i++)
		//	{
		//		LinkSegmentOld segment = normalSegments[i];
		//		if (currentSegments.Any(s => s.Target == segment.Target))
		//		{
		//			a2 = i;
		//			break;
		//		}
		//	}

		//	List<LinkSegmentOld> segments = new List<LinkSegmentOld>();
		//	for (int i = a1; i <= a2; i++)
		//	{
		//		segments.Add(normalSegments[i]);
		//	}

		//	return segments;
		//}


		//public IReadOnlyList<LinkSegmentOld> GetZoomedOutBeforeReplacedSegments(
		//	IReadOnlyList<LinkSegmentOld> normalSegments,
		//	IReadOnlyList<LinkSegmentOld> currentSegments,
		//	NodeOld source,
		//	NodeOld target)
		//{
		//	int index1 = normalSegments.TakeWhile(s => s.Source != source).Count() + 1;
		//	int a1 = 0;
		//	for (int i = index1; i > -1; i--)
		//	{
		//		LinkSegmentOld segment = normalSegments[i];
		//		if (currentSegments.Any(s => s.Source == segment.Source))
		//		{
		//			a1 = i;
		//			break;
		//		}
		//	}

		//	int index2 = normalSegments.TakeWhile(s => s.Target != target).Count();
		//	int a2 = 0;
		//	for (int i = index2; i < normalSegments.Count; i++)
		//	{
		//		LinkSegmentOld segment = normalSegments[i];
		//		if (currentSegments.Any(s => s.Target == segment.Target))
		//		{
		//			a2 = i;
		//			break;
		//		}
		//	}

		//	List<LinkSegmentOld> segments = new List<LinkSegmentOld>();
		//	for (int i = a1; i <= a2; i++)
		//	{
		//		segments.Add(normalSegments[i]);
		//	}

		//	return segments;
		//}
	}
}