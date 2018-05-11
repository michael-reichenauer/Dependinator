namespace Dependinator.ModelViewing.ModelHandling.Core
{
	internal class Edge : IEdge
	{
		public Edge(Node source, Node target)
		{
			Source = source;
			Target = target;
		}


		public Node Source { get; }
		public Node Target { get; }
	}
}