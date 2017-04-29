using System.Collections.Generic;
using System.Linq;
using Dependiator.Modeling.Nodes;


namespace Dependiator.Modeling.Links
{
	internal class NodeLinks
	{
		private readonly ILinkService linkService;
		private readonly List<Link> links = new List<Link>();
		private readonly List<LinkSegment> ownedSegments = new List<LinkSegment>();
		private readonly List<LinkSegment> referencingSegments = new List<LinkSegment>();
	

		public IReadOnlyList<Link> Links => links;

		public IReadOnlyList<LinkSegment> OwnedSegments => ownedSegments;

		public IReadOnlyList<LinkSegment> ReferencingSegments => referencingSegments;


		public NodeLinks(ILinkService linkService)
		{
			this.linkService = linkService;
		}


		public void AddDirectLink(Node groupSource, Node groupTarget, IReadOnlyList<Link> groupLinks)
		{

		}



		public void Add(Link link)
		{
			if (links.Contains(link))
			{
				return;
			}

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

				// Go to next segment in the line segments 
				segmentSource = segmentTarget;
			}
		}

		
		private void AddSegment(Node source, Node target, Link link)
		{
			Node segmentOwner = GetLinkSegmentOwner(source, target);

			LinkSegment segment = segmentOwner.Links.ownedSegments
				.FirstOrDefault(l => l.Source == source && l.Target == target);

			if (segment == null)
			{
				segment = new LinkSegment(linkService, source, target, segmentOwner);

				AddSegment(segment);
			}

			segment.Add(link);
			link.Add(segment);
		}


		private static void AddSegment(LinkSegment segment)
		{
			segment.Owner.Links.AddOwnedSegment(segment);
			segment.Source.Links.AddReferencedSegment(segment);
			segment.Target.Links.AddReferencedSegment(segment);
		}


		public void AddOwnedSegment(LinkSegment segment)
		{
			if (!ownedSegments.Contains(segment))
			{
				ownedSegments.Add(segment);
			}
		}


		public void AddReferencedSegment(LinkSegment segment)
		{
			if (!referencingSegments.Contains(segment))
			{
				referencingSegments.Add(segment);
			}
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