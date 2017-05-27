using System.Collections.Generic;
using System.Linq;
using Dependiator.Modeling.Nodes;
using Dependiator.Utils;


namespace Dependiator.Modeling.Links
{
	internal class Link : Equatable<Link>
	{
		private readonly List<LinkLine> lines = new List<LinkLine>();
		private IReadOnlyList<LinkSegment> currentLinkSegments;

		public Link(Node source, Node target)
		{
			Source = source;
			Target = target;
		}


		public Node Source { get; }

		public Node Target { get; }

		public IReadOnlyList<LinkLine> Lines => lines;
		public IReadOnlyList<LinkSegment> LinkSegments => currentLinkSegments;


		public bool TryAddLinkLine(LinkLine line) => lines.TryAdd(line);


		public bool Remove(LinkLine line) => lines.Remove(line);


		protected override bool IsEqual(Link other) => Source == other.Source && Target == other.Target;
		protected override int GetHash() => GetHashes(Source, Target);



		public override string ToString() => $"{Source} -> {Target}";


		public void SetLinkSegments(IReadOnlyList<LinkSegment> linkSegments)
		{
			currentLinkSegments = linkSegments;
		}
	}
}