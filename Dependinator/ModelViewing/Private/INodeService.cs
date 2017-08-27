using Dependinator.ModelParsing;

namespace Dependinator.ModelViewing.Private
{
	internal interface INodeService
	{
		void UpdateNode(ModelNode modelNode, int id);
		void RemoveObsoleteNodesAndLinks(int stamp);
		void RemoveAll();
		void ResetLayout();
	}
}