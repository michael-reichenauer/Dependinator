using Dependinator.ModelParsing;

namespace Dependinator.ModelViewing.Private
{
	internal interface INodeService
	{
		void UpdateNode(DataNode dataNode, int id);
		void RemoveObsoleteNodesAndLinks(int stamp);
		void RemoveAll();
		void ResetLayout();
	}
}