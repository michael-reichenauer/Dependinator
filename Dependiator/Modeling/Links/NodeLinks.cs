using System.Collections.Generic;
using System.Linq;
using Dependiator.Modeling.Nodes;


namespace Dependiator.Modeling.Links
{
	internal class NodeLinks
	{
		private readonly ILinkService linkService;
		private readonly List<Link> links = new List<Link>();
		private readonly List<LinkLine> ownedLines = new List<LinkLine>();
		private readonly List<LinkLine> referencingLines = new List<LinkLine>();


		public IReadOnlyList<Link> Links => links;

		public IReadOnlyList<LinkLine> OwnedLines => ownedLines;

		public IReadOnlyList<LinkLine> ReferencingLines => referencingLines;


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



		private static void AddSegmentLink(LinkLine line, Link link)
		{
			LinkLine existingLine =
				line.Owner.Links.ownedLines.Find(s => s == line);

			if (existingLine == null)
			{
				AddLinkLine(line);
				existingLine = line;
			}

			existingLine.TryAddLink(link);
			link.TryAdd(existingLine);
		}


		private static void AddLinkLine(LinkLine line)
		{
			line.Owner.Links.TryAddOwnedLine(line);
			line.Source.Links.TryAddReferencedLine(line.Source, line);
			line.Target.Links.TryAddReferencedLine(line.Target, line);
		}


		public bool TryAddOwnedLine(LinkLine line) => ownedLines.TryAdd(line);


		public bool TryAddReferencedLine(Node referencingNode, LinkLine line)
		{
			if (referencingLines.TryAdd(line))
			{
				line.TryAddReferencingNode(referencingNode);
				return true;
			}

			return false;
		}



		public override string ToString() => $"{ownedLines.Count} links";
	}
}