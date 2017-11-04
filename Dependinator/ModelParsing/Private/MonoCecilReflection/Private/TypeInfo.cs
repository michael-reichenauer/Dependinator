using Mono.Cecil;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
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