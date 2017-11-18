using Dependinator.ModelParsing;

namespace Dependinator.ModelViewing.Private
{
	internal interface IModelNodeService
	{
		void UpdateNode(ModelNode modelNode, int id);
		void RemoveObsoleteNodesAndLinks(int stamp);
		void SetLayoutDone();
		void RemoveAll();
	}
}