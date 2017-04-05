using System.Collections.Generic;
using System.Linq;
using Dependiator.Utils;


namespace Dependiator.Modeling
{
	internal class NodeLinks : IEnumerable<Link>
	{
		//private readonly IItemService itemService;
		private readonly Node ownerNode;
		private readonly List<Link> links = new List<Link>();


		public NodeLinks(Node ownerNode)
		{
			this.ownerNode = ownerNode;
		}


		public int Count => links.Count;

		public IEnumerable<Link> SourceReferences => links
			.Where(r => r.Source == ownerNode);

		public IEnumerable<Link> TargetReferences => links
			.Where(r => r.Target == ownerNode);



		public void Add(NodeLink nodeLink)
		{
			Asserter.Requires(nodeLink.Source == ownerNode);

			AddLineSegments(nodeLink);
		}


		private void AddLineSegments(NodeLink nodeLink)
		{
			// Start with first segment at the start of the line 
			Node segmentSource = nodeLink.Source;

			// Iterate segments until line end is reached
			while (segmentSource != nodeLink.Target)
			{
				// Try to assume next segment target is a child node by searching if segment source
				// is a ancestor of end target node
				Node segmentTarget = nodeLink.Target.AncestorsAndSelf()
					.FirstOrDefault(ancestor => ancestor.ParentNode == segmentSource);

				if (segmentTarget == null)
				{
					// Not a child, lets try to assume target is a sibling node
					segmentTarget = nodeLink.Target.AncestorsAndSelf()
						.FirstOrDefault(ancestor => ancestor.ParentNode == segmentSource.ParentNode);
				}

				if (segmentTarget == null)
				{
					// Neither child not sibling, then next segment target node must be the parent node
					segmentTarget = segmentSource.ParentNode;
				}

				AddSegment(segmentSource, segmentTarget, nodeLink);

				// Goto next segment in the line segments 
				segmentSource = segmentTarget;
			}
		}


		private void AddSegment(Node source, Node target, NodeLink nodeLink)
		{
			// Try to check if source node already contains this segment link
			Link segment = source.Links.FirstOrDefault(
				l => l.Source == source && l.Target == target);

			if (segment == null)
			{
				// No existing segment link, create the link and add to both source and target node
				segment = new Link(source, target, ownerNode);
				source.Links.AddLinkSegment(segment);
				target.Links.AddLinkSegment(segment);
			}

			segment.Add(nodeLink);
		}


		private void AddLinkSegment(Link segment)
		{
			if (links.Any(l => l.Source == segment.Source && l.Target == segment.Target))
			{
				return;
			}

			links.Add(segment);
		}


		public IEnumerable<Link> DescendentAndSelfSourceReferences()
		{
			foreach (Node node in ownerNode.DescendentsAndSelf())
			{
				foreach (Link reference in node.Links.SourceReferences)
				{
					yield return reference;
				}
			}
		}


		public IEnumerable<Link> DescendentAndSelfTargetReferences()
		{
			foreach (Node node in ownerNode.DescendentsAndSelf())
			{
				foreach (Link reference in node.Links.TargetReferences)
				{
					yield return reference;
				}
			}
		}


		public IEnumerator<Link> GetEnumerator()
		{
			return links.GetEnumerator();
		}


		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public override string ToString() => $"{links.Count} references";
	}
}