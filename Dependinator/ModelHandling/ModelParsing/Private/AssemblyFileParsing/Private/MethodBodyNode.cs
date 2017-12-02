using Dependinator.ModelHandling.Core;
using Mono.Cecil;


namespace Dependinator.ModelHandling.ModelParsing.Private.AssemblyFileParsing.Private
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