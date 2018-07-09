using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.ModelHandling.Private
{
	internal class LinkSegment : IEdge
	{
		public LinkSegment(Node source, Node target)
		{
			Source = source;
			Target = target;
		}


		public Node Source { get; }
		public Node Target { get; }

		public override string ToString() => $"{Source}->{Target}";
	}
}