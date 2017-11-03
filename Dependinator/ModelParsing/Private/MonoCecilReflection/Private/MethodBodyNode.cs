using Mono.Cecil;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
{
	internal class MethodBodyNode
	{
		public ModelNode MemberNode { get; }
		public MethodDefinition Method { get; }


		public MethodBodyNode(ModelNode memberNode, MethodDefinition method)
		{
			MemberNode = memberNode;
			Method = method;
		}
	}
}