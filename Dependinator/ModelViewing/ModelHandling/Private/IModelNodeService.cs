using System.Collections.Generic;
using Dependinator.ModelViewing.DataHandling;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal interface IModelNodeService
	{
		void UpdateNode(DataNode dataNode, int id);
		void RemoveObsoleteNodesAndLinks(int stamp);
		void SetLayoutDone();
		void ShowHiddenNode(NodeName nodeName);
		IReadOnlyList<NodeName> GetHiddenNodeNames();
		void HideNode(Node node);
		void RemoveAll();
	}
}