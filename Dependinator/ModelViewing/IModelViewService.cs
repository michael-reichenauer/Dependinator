using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.Common;
using Dependinator.ModelViewing.Items;


namespace Dependinator.ModelViewing
{
	internal interface IModelViewService
	{
		void SetRootCanvas(ItemsCanvas rootCanvas);

		Task LoadAsync();

		Task RefreshAsync(bool refreshLayout);

		void ShowHiddenNode(NodeName nodeName);

		void Close();
		IReadOnlyList<NodeName> GetHiddenNodeNames();
		void Clicked();
	}
}