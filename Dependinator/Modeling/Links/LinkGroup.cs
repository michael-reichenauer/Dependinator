using System.Collections.Generic;
using Dependiator.Modeling.Nodes;


namespace Dependiator.Modeling.Links
{
	internal class LinkGroup
	{
		public LinkGroup(Node source, Node target, IReadOnlyList<Link> links)
		{
			Source = source;
			Target = target;
			Links = links;
		}


		public Node Source { get; }
		public Node Target { get; }
		public IReadOnlyList<Link> Links { get; }

		public override string ToString() => $"{Source} -> {Target} ({Links.Count})";
	}
}