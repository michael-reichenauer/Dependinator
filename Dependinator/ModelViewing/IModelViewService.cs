using System.Collections.Generic;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Searching;


namespace Dependinator.ModelViewing
{
	internal interface IModelViewService
	{
		void StartMoveToNode(NodeName nodeName);

		IReadOnlyList<NodeName> GetHiddenNodeNames();

		void ShowHiddenNode(NodeName nodeName);

		IEnumerable<SearchEntry> Search(string text);
	}
}