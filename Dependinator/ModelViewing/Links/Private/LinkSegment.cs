using Dependinator.ModelViewing.Nodes;

namespace Dependinator.ModelViewing.Links.Private
{
	internal class LinkSegment
	{
		public LinkSegment(Node source, Node target, Link link)
		{
			Source = source;
			Target = target;
			Link = link;
		}


		public Node Source { get; }
		public Node Target { get; }
		public Link Link { get; }

		public override string ToString() => $"{Source}->{Target} ({Link})";
	}
}