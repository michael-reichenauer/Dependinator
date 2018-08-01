using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.ItemsViewing;
using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.ModelHandling
{
	internal interface IModelHandlingService
	{
		void SetRootCanvas(ItemsCanvas rootCanvas);

		Task LoadAsync();

		Task RefreshAsync(bool refreshLayout);

		Node Root { get; }

		void ShowHiddenNode(NodeName nodeName);

		IReadOnlyList<NodeName> GetHiddenNodeNames();

		Task CloseAsync();
	}
}
