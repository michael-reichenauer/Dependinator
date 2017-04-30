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
				.ForEach(segment => AddSegmentLink(segment, link));
		}



		private static void AddSegmentLink(LinkSegment segment, Link link)
		{
			LinkSegment existingSegment =
				segment.Owner.Links.ownedSegments.Find(s => s == segment);

			if (existingSegment == null)
			{
				AddSegment(segment);
				existingSegment = segment;
			}

			existingSegment.TryAddLink(link);
			link.TryAdd(existingSegment);
		}


		private static void AddSegment(LinkSegment segment)
		{
			segment.Owner.Links.TryAddOwnedSegment(segment);
			segment.Source.Links.TryAddReferencedSegment(segment.Source, segment);
			segment.Target.Links.TryAddReferencedSegment(segment.Target, segment);
		}


		public bool TryAddOwnedSegment(LinkSegment segment) => ownedSegments.TryAdd(segment);


		public bool TryAddReferencedSegment(Node referencingNode, LinkSegment segment)
		{
			if (referencingSegments.TryAdd(segment))
			{
				segment.TryAddReferencingNode(referencingNode);
				return true;
			}

			return false;
		}



		public override string ToString() => $"{ownedSegments.Count} links";
	}
}