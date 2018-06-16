using Mono.Cecil;


namespace Dependinator.ModelViewing.ModelDataHandling.Private.ModelParsing.Private.AssemblyFileParsing.Private
{
	internal class TypeInfo
	{
		public TypeDefinition Type { get; }
		public DataNode Node { get; }
		public bool IsAsyncStateType { get; }


		public TypeInfo(TypeDefinition type, DataNode node, bool isAsyncStateType)
		{
			Type = type;
			Node = node;
			IsAsyncStateType = isAsyncStateType;
		}
	}
}