using System.Collections.Generic;
using Dependinator.ModelViewing.ModelDataHandling;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal interface IModelNodeService
	{
		void UpdateNode(DataNode dataNode, int id);
		void RemoveObsoleteNodesAndLinks(int stamp);
		void SetLayoutDone();
		void RemoveAll();
		void ShowHiddenNode(NodeName nodeName);
		IReadOnlyList<NodeName> GetHiddenNodeNames();
		void HideNode(Node node);
	}
}