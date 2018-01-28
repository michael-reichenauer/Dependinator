namespace Dependinator.ModelViewing.ModelHandling.Core
{
	internal interface IEdge
	{
		Node Source { get; }
		Node Target { get; }
	}
}