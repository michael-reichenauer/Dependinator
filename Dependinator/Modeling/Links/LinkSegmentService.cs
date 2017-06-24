using System.Collections.Generic;
using System.Linq;
using Dependinator.Modeling.Nodes;


namespace Dependinator.Modeling.Links
{
	internal class LinkSegmentService : ILinkSegmentService
	{


		public IReadOnlyList<LinkSegment> GetNormalLinkSegments(Link link)
		{
			List<LinkSegment> segments = new List<LinkSegment>();

			// Start with first line at the start of the segmented line 
			Node segmentSource = link.Source;

			// Iterate segments until line end is reached
			while (segmentSource != link.Target)
			{
				// Try to assume next line target is a child node by searching if line source
				// is a ancestor of end target node
				Node segmentTarget = link.Target.AncestorsAndSelf()
					.FirstOrDefault(ancestor => ancestor.ParentNode == segmentSource);

				if (segmentTarget == null)
				{
					// Segment target was not a child, lets try to assume target is a sibling node
					segmentTarget = link.Target.AncestorsAndSelf()
						.FirstOrDefault(ancestor => ancestor.ParentNode == segmentSource.ParentNode);
				}

				if (segmentTarget == null)
				{
					// Segment target was neither child nor a sibling, next line target node must
					// be the parent node
					segmentTarget = segmentSource.ParentNode;
				}

				Node segmentOwner = segmentSource == segmentTarget.ParentNode ? segmentSource : segmentSource.ParentNode;
				LinkSegment segment = new LinkSegment(segmentSource, segmentTarget, segmentOwner, link);

				segments.Add(segment);

				// Go to next line in the line segments 
				segmentSource = segmentTarget;
			}

			return segments;
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


		public IReadOnlyList<LinkSegment> GetNewLinkSegments(
			IReadOnlyList<LinkSegment> linkSegments,
			IReadOnlyList<LinkSegment> newSegments)
		{
			Node source = newSegments.First().Source;
			Node target = newSegments.Last().Target;

			// Get the segments that are before the new segment
			IEnumerable<LinkSegment> preSegments = linkSegments
				.TakeWhile(segment => segment.Source != source);

			// Get the segments that are after the new segments
			IEnumerable<LinkSegment> postSegments = linkSegments
				.SkipWhile(segment => segment.Source != target);

			return
				preSegments
					.Concat(newSegments)
					.Concat(postSegments)
					.ToList();
		}


		public LinkSegment GetZoomedInSegment(
			IReadOnlyList<LinkSegment> replacedSegments, Link link)
		{
			Node source = replacedSegments.First().Source;
			Node target = replacedSegments.Last().Target;

			Node segmentOwner = source.AncestorsAndSelf()
				.First(node => target.AncestorsAndSelf().Contains(node));

			return new LinkSegment(source, target, segmentOwner, link);
		}



		public IReadOnlyList<LinkSegment> GetZoomedInReplacedSegments(
			IEnumerable<LinkSegment> linkSegments,
			Node source,
			Node target)
		{
			// Get the segments that are one before the line and one after the line
			return linkSegments
				.SkipWhile(segment => segment.Source != source && segment.Target != source)
				.TakeWhile(segment =>
					segment.Target == source || segment.Source == source || segment.Source == target)
				.ToList();
		}

		public IReadOnlyList<LinkSegment> GetZoomedInBeforeReplacedSegments(
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



		public IReadOnlyList<LinkSegment> GetZoomedInAfterReplacedSegments(
			IEnumerable<LinkSegment> linkSegments,
			Node source,
			Node target)
		{
			// Get the segments that are one after the line
			return linkSegments
				.SkipWhile(segment => segment.Source != source)
				.TakeWhile(segment => segment.Source == source || segment.Source == target)
				.ToList();
		}


		public IReadOnlyList<LinkSegment> GetZoomedOutReplacedSegments(
			IReadOnlyList<LinkSegment> normalSegments,
			IReadOnlyList<LinkSegment> currentSegments,
			Node source,
			Node target)
		{
			int index1 = normalSegments.TakeWhile(s => s.Source != source).Count();
			int a1 = 0;
			for (int i = index1; i > -1; i--)
			{
				LinkSegment segment = normalSegments[i];
				if (currentSegments.Any(s => s.Source == segment.Source))
				{
					a1 = i;
					break;
				}
			}

			int index2 = normalSegments.TakeWhile(s => s.Target != target).Count();
			int a2 = 0;
			for (int i = index2; i < normalSegments.Count; i++)
			{
				LinkSegment segment = normalSegments[i];
				if (currentSegments.Any(s => s.Target == segment.Target))
				{
					a2 = i;
					break;
				}
			}

			List<LinkSegment> segments = new List<LinkSegment>();
			for (int i = a1; i <= a2; i++)
			{
				segments.Add(normalSegments[i]);
			}

			return segments;
		}


		public IReadOnlyList<LinkSegment> GetZoomedOutBeforeReplacedSegments(
			IReadOnlyList<LinkSegment> normalSegments,
			IReadOnlyList<LinkSegment> currentSegments,
			Node source,
			Node target)
		{
			int index1 = normalSegments.TakeWhile(s => s.Source != source).Count() + 1;
			int a1 = 0;
			for (int i = index1; i > -1; i--)
			{
				LinkSegment segment = normalSegments[i];
				if (currentSegments.Any(s => s.Source == segment.Source))
				{
					a1 = i;
					break;
				}
			}

			int index2 = normalSegments.TakeWhile(s => s.Target != target).Count();
			int a2 = 0;
			for (int i = index2; i < normalSegments.Count; i++)
			{
				LinkSegment segment = normalSegments[i];
				if (currentSegments.Any(s => s.Target == segment.Target))
				{
					a2 = i;
					break;
				}
			}

			List<LinkSegment> segments = new List<LinkSegment>();
			for (int i = a1; i <= a2; i++)
			{
				segments.Add(normalSegments[i]);
			}

			return segments;
		}
	}
}