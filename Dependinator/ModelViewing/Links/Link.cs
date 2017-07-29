using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Links
{
	internal class Link : Equatable<Link>
	{
		public Link(Node source, Node target, LinkId linkId)
		{
			Id = linkId;
			Source = source;
			Target = target;

			IsEqualWhen(Id);
		}

		public LinkId Id { get; }
		public Node Target { get; }
		public Node Source { get; }

		public override string ToString() => Id.ToString();
	}
}