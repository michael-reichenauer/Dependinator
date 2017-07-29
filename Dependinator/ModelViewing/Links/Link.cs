using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Links
{
	internal class Link : Equatable<Link>
	{
		public Link(Node source, Node target)
		{
			Source = source;
			Target = target;

			IsEqualWhen(source, target);
		}

		public Node Target { get; }
		public Node Source { get; }

		public override string ToString() => $"{Source}->{Target}";
	}
}