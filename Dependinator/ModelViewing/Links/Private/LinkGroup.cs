using System.Collections.Generic;
using Dependinator.ModelViewing.Nodes;

namespace Dependinator.ModelViewing.Links.Private
{
	internal class LinkGroup
	{
		public LinkGroup(NodeOld source, NodeOld target, IReadOnlyList<LinkOld> links)
		{
			Source = source;
			Target = target;
			Links = links;
		}


		public NodeOld Source { get; }
		public NodeOld Target { get; }
		public IReadOnlyList<LinkOld> Links { get; }

		public override string ToString() => $"{Source} -> {Target} ({Links.Count})";
	}
}