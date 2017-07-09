using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.Links.Private;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Links
{
	internal class LinkOld : Equatable<LinkOld>
	{
		private readonly List<LinkLine> lines = new List<LinkLine>();
		private IReadOnlyList<LinkSegment> currentLinkSegments;

		public LinkOld(NodeOld source, NodeOld target)
		{
			Source = source;
			Target = target;
			IsEqualWhen(Source, Target);
		}


		public NodeOld Source { get; }

		public NodeOld Target { get; }

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