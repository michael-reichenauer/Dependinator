using System.Collections.Generic;
using Dependiator.Modeling.Nodes;
using Dependiator.Utils;


namespace Dependiator.Modeling.Links
{
	internal class Link : Equatable<Link>
	{
		private readonly List<LinkSegment> segments = new List<LinkSegment>();

		public Link(Node source, Node target)
		{
			Source = source;
			Target = target;
		}

		public Node Source { get; }

		public Node Target { get; }


		public void Add(LinkSegment segment)
		{
			if (!segments.Contains(segment))
			{
				segments.Add(segment);
			}
		}



		protected override bool IsEqual(Link other) => Source == other.Source && Target == other.Target;

		public override string ToString() => $"{Source} -> {Target}";
	}
}