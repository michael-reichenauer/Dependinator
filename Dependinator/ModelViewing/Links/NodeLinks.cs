using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.Nodes;


namespace Dependinator.ModelViewing.Links
{
	internal class NodeLinks
	{
		private readonly ILinkService linkService;
		private readonly List<LinkOld> links = new List<LinkOld>();
		private readonly List<LinkLine> ownedLines = new List<LinkLine>();
		private readonly List<LinkLine> referencingLines = new List<LinkLine>();


		public IReadOnlyList<LinkOld> Links => links;

		public IReadOnlyList<LinkLine> OwnedLines => ownedLines;

		public IReadOnlyList<LinkLine> ReferencingLines => referencingLines;


		public NodeLinks(ILinkService linkService)
		{
			this.linkService = linkService;
		}


		public void AddDirectLink(NodeOld groupSource, NodeOld groupTarget, IReadOnlyList<LinkOld> groupLinks)
		{

		}


		public void Add(LinkOld link)
		{
			if (links.Contains(link))
			{
				return;
			}

			links.Add(link);

			linkService.AddLinkLines(link);
		}
	

		public bool TryAddOwnedLine(LinkLine line) => ownedLines.TryAdd(line);

		public bool RemoveOwnedLine(LinkLine line) => ownedLines.Remove(line);

		public bool RemoveReferencedLine(LinkLine line) => referencingLines.Remove(line);


		public bool TryAddReferencedLine(LinkLine line) => referencingLines.TryAdd(line);

		public override string ToString() => $"{ownedLines.Count} links";
	}
}