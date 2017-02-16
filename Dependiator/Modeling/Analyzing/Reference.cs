using System.Collections.Generic;
using System.Linq;


namespace Dependiator.Modeling.Analyzing
{
	internal class LinkGroup
	{
		private List<LinkX> subLinks = new List<LinkX>();

		public LinkGroup(Element source, Element target)
		{
			Source = source;
			Target = target;
		}

		public Element Source { get; }

		public Element Target { get; }

		public IReadOnlyList<LinkX> SubLinks => subLinks;


		public void Add(LinkX link)
		{
			if (subLinks
				.Any(l => l.Source == link.Source && l.Target == link.Target && l.Kind == link.Kind))
			{
				return;
			}

			subLinks.Add(link);
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