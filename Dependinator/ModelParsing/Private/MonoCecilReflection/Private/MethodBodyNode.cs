using Mono.Cecil;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
{
	internal class MethodBodyNode
	{
		public ModelNode MemberNode { get; }
		public MethodDefinition Method { get; }
		public bool IsMoveNext { get; }


		public MethodBodyNode(ModelNode memberNode, MethodDefinition method, bool isMoveNext)
		{
			MemberNode = memberNode;
			Method = method;
			IsMoveNext = isMoveNext;
		}
	}
}