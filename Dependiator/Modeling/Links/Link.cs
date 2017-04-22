using Dependiator.Utils;


namespace Dependiator.Modeling.Links
{
	internal class Link : Equatable<Link>
	{
		public Link(Node source, Node target)
		{
			Source = source;
			Target = target;
		}

		public Node Source { get; }

		public Node Target { get; }


		protected override bool IsEqual(Link other) => Source == other.Source && Target == other.Target;

		protected override int GetHash() => GetHashes(Source, Target);

		public override string ToString() => $"{Source} -> {Target}";
	}
}