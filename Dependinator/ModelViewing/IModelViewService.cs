using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Nodes;


namespace Dependinator.ModelViewing
{
	internal interface IModelViewService
	{
		void StartMoveToNode(NodeName nodeName);

		IReadOnlyList<NodeName> GetHiddenNodeNames();

		void ShowHiddenNode(NodeName nodeName);

		IEnumerable<NodeName> Search(string text);
		Task ActivateRefreshAsync();
		Task RefreshAsync(bool refreshLayout);
		void Close();
	}
}