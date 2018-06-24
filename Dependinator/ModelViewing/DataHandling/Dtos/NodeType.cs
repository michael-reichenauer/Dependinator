namespace Dependinator.ModelViewing.DataHandling.Dtos
{
	internal enum NodeType
	{
		None,
		Module,
		NameSpace,
		Type,
		Member,
	}


	internal static class NodeTypeExtensions
	{
		public static bool IsGroup(this NodeType n1) =>
			n1 == NodeType.Module || n1 == NodeType.NameSpace;
	}

}