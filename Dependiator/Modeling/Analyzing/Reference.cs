using System.Collections.Generic;
using System.Linq;


namespace Dependiator.Modeling.Analyzing
{
	internal class LinkGroup
	{
		private List<LinkX> links = new List<LinkX>();

		public LinkGroup(Element source, Element target)
		{
			Source = source;
			Target = target;
		}

		public Element Source { get; }

		public Element Target { get; }

		public IReadOnlyList<LinkX> Links => links;


		public void Add(LinkX link)
		{
			if (links
				.Any(l => l.Source == link.Source && l.Target == link.Target && l.Kind == link.Kind))
			{
				return;
			}

			links.Add(link);
		}

		public override string ToString() => $"{Source} -> {Target}";
	}


	internal class LinkX
	{
		public LinkX(Element source, Element target, LinkKind kind)
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