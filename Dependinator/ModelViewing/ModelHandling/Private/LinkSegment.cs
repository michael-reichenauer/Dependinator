using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal class LinkSegment : IEdge
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