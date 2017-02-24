namespace Dependiator.Modeling
{
	internal class NodeLink
	{
		public NodeLink(Node source, Node target)
		{
			Source = source;
			Target = target;
		}

		public Node Source { get; }

		public Node Target { get; }

		public override string ToString() => $"{Source} -> {Target}";
	}
}