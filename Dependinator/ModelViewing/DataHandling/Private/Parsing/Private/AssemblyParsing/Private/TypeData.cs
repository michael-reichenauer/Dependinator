using Dependinator.ModelViewing.DataHandling.Dtos;
using Mono.Cecil;


namespace Dependinator.ModelViewing.DataHandling.Private.Parsing.Private.AssemblyParsing.Private
{
	internal class TypeData
	{
		public TypeDefinition Type { get; }
		public DataNode Node { get; }
		public bool IsAsyncStateType { get; }


		public TypeData(TypeDefinition type, DataNode node, bool isAsyncStateType)
		{
			Type = type;
			Node = node;
			IsAsyncStateType = isAsyncStateType;
		}
	}
}