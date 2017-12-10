using Dependinator.ModelHandling.Core;
using Mono.Cecil;


namespace Dependinator.ModelHandling.Private.ModelParsing.Private.AssemblyFileParsing.Private
{
	internal class TypeInfo
	{
		public TypeDefinition Type { get; }
		public ModelNode Node { get; }
		public bool IsAsyncStateType { get; }


		public TypeInfo(TypeDefinition type, ModelNode node, bool isAsyncStateType)
		{
			Type = type;
			Node = node;
			IsAsyncStateType = isAsyncStateType;
		}
	}
}