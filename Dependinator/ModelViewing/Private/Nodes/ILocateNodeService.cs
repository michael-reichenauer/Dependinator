namespace Dependinator.ModelViewing.Private.Nodes
{
	internal interface ILocateNodeService
	{
		bool TryStartMoveToNode(NodeName nodeName);
	}
}