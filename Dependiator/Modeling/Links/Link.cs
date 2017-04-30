using System.Collections.Generic;
using System.Linq;
using Dependiator.Modeling.Nodes;
using Dependiator.Utils;


namespace Dependiator.Modeling.Links
{
	internal class Link : Equatable<Link>
	{
		private readonly List<LinkLine> lines = new List<LinkLine>();

		public Link(Node source, Node target)
		{
			Source = source;
			Target = target;
		}


		public Node Source { get; }

		public Node Target { get; }


		public bool TryAdd(LinkLine line) => lines.TryAdd(line);


		public bool Remove(LinkLine line) => lines.Remove(line);


		protected override bool IsEqual(Link other) => Source == other.Source && Target == other.Target;

		public override string ToString() => $"{Source} -> {Target}";
	}
}