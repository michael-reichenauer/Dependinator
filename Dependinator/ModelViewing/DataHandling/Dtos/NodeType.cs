namespace Dependinator.ModelViewing.DataHandling.Dtos
{
	internal enum NodeType
	{
		None,
		Solution,
		Project,
		Group,
		Dll,
		Exe,
		NameSpace,
		Type,
		Member,
	}

	internal static class NodeTypeExtensions
	{
		public static bool IsNamespace(this NodeType n1) => !(n1.IsType() || n1.IsMember());

		public static bool IsType(this NodeType n1) => n1 == NodeType.Type;

		public static bool IsMember(this NodeType n1) => n1 == NodeType.Member;
	}
}