namespace Dependinator.ModelViewing.Private.ModelHandling.Core
{
	internal interface IEdge
	{
		Node Source { get; }
		Node Target { get; }
	}
}