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

			linkService
				.GetLinkSegments(link)
				.ForEach(segment => AddSegment(segment, link));
		}



		private static void AddSegment(LinkSegment segment, Link link)
		{
			LinkSegment existingSegment =
				segment.Owner.Links.ownedSegments.Find(s => s == segment);

			if (existingSegment == null)
			{
				AddSegment(segment);
				existingSegment = segment;
			}

			existingSegment.Add(link);
			link.Add(existingSegment);
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