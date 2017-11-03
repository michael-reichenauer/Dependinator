using Mono.Cecil;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
{
	internal class TypeInfo
	{
		public TypeDefinition Type { get; }
		public ModelNode Node { get; }

		public TypeInfo(TypeDefinition type, ModelNode node)
		{
			Type = type;
			Node = node;
		}
	}
}