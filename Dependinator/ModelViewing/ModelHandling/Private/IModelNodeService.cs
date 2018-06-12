using System.Collections.Generic;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal interface IModelNodeService
	{
		void UpdateNode(ModelNode modelNode, int id);
		void RemoveObsoleteNodesAndLinks(int stamp);
		void SetLayoutDone();
		void RemoveAll();
		void ShowHiddenNode(NodeName nodeName);
		IReadOnlyList<NodeName> GetHiddenNodeNames();
	}
}