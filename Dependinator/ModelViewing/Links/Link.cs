using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.Links.Private;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Links
{
	internal class Link : Equatable<Link>
	{
		private readonly List<LinkLine> lines = new List<LinkLine>();
		private IReadOnlyList<LinkSegment> currentLinkSegments;

		public Link(Node source, Node target)
		{
			Source = source;
			Target = target;
			IsEqualWhen(Source, Target);
		}


		public Node Source { get; }

		public Node Target { get; }

		public IReadOnlyList<LinkLine> Lines => lines;
		public IReadOnlyList<LinkSegment> LinkSegments => currentLinkSegments;


		public bool TryAddLinkLine(LinkLine line) => lines.TryAdd(line);

		public bool Remove(LinkLine line) => lines.Remove(line);


		public override string ToString() => $"{Source} -> {Target}";


		public void SetLinkSegments(IReadOnlyList<LinkSegment> linkSegments)
		{
			currentLinkSegments = linkSegments;
		}
	}
}