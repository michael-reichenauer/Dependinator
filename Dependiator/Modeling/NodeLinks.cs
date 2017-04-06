using System.Collections.Generic;
using System.Linq;
using Dependiator.Utils;


namespace Dependiator.Modeling
{
	internal class NodeLinks
	{
		private readonly List<Link> links = new List<Link>();
		private readonly List<LinkSegment> ownedSegments = new List<LinkSegment>();
		private readonly List<LinkSegment> sourceSegments = new List<LinkSegment>();
		private readonly List<LinkSegment> targetSegments = new List<LinkSegment>();


		public IReadOnlyList<Link> Links => links;

		public IReadOnlyList<LinkSegment> OwnedSegments => ownedSegments;

		public IReadOnlyList<LinkSegment> SourceSegments => sourceSegments;

		public IReadOnlyList<LinkSegment> TargetSegments => targetSegments;


		public void Add(Link link)
		{
			links.Add(link);

			// Start with first segment at the start of the segmented line 
			Node segmentSource = link.Source;

			// Iterate segments until line end is reached
			while (segmentSource != link.Target)
			{
				// Try to assume next segment target is a child node by searching if segment source
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
					// Segment target was neither child nor a sibling, next segment target node must
					// be the parent node
					segmentTarget = segmentSource.ParentNode;
				}

				AddSegment(segmentSource, segmentTarget, link);

				// Goto next segment in the line segments 
				segmentSource = segmentTarget;
			}
		}

		
		private static void AddSegment(Node source, Node target, Link link)
		{
			Node segmentOwner = GetLinkSegmentOwner(source, target);

			LinkSegment segment = segmentOwner.Links.ownedSegments
				.FirstOrDefault(l => l.Source == source && l.Target == target);

			if (segment == null)
			{
				segment = new LinkSegment(source, target, segmentOwner);

				segmentOwner.Links.ownedSegments.Add(segment);
				source.Links.sourceSegments.Add(segment);
				target.Links.targetSegments.Add(segment);
			}

			segment.Add(link);
		}


		private static Node GetLinkSegmentOwner(Node source, Node target)
		{
			if (source == target.ParentNode)
			{
				// The target is the child of the target, let the source own the segment
				return source;
			}
			else
			{
				// The target is either a sibling or a parent of the source, let the source parent own
				return source.ParentNode;
			}
		}



		public override string ToString() => $"{ownedSegments.Count} links";
	}
}