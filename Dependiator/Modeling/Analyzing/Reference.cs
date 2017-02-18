using System.Collections.Generic;
using System.Linq;


namespace Dependiator.Modeling.Analyzing
{
	internal class LinkGroup
	{
		private List<NodeLink> links = new List<NodeLink>();

		public LinkGroup(Element source, Element target)
		{
			Source = source;
			Target = target;
		}

		public Element Source { get; }

		public Element Target { get; }

		public IReadOnlyList<NodeLink> Links => links;


		public void Add(NodeLink nodeLink)
		{
			if (links
				.Any(l => l.Source == nodeLink.Source && l.Target == nodeLink.Target && l.Kind == nodeLink.Kind))
			{
				return;
			}

			links.Add(nodeLink);
		}

		public override string ToString() => $"{Source} -> {Target}";
	}


	internal class NodeLink
	{
		public NodeLink(Element source, Element target, LinkKind kind)
		{
			Source = source;
			Target = target;
			Kind = kind;
		}

		public Element Source { get; }

		public Element Target { get; }

		public LinkKind Kind { get; }

		public override string ToString() => $"{Source} -> {Target}";
	}
}