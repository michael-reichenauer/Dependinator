using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private.ModelHandling.Core
{
	internal class Link : Equatable<Link>, IEdge
	{
		public Link(Node source, Node target)
		{
			Source = source;
			Target = target;

			IsEqualWhenSame(source, target);
		}

		public int Stamp { get; set; }
		public Node Target { get; }
		public Node Source { get; }

		public bool IsHidden => Source.View.IsHidden || Target.View.IsHidden;

		public override string ToString() => $"{Source}->{Target}";
	}
}