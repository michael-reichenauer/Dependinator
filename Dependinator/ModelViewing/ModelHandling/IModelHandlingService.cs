using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.Nodes;


namespace Dependinator.ModelViewing.ModelHandling
{
	internal interface IModelHandlingService
	{
		void SetRootCanvas(ItemsCanvas rootCanvas);

		Task LoadAsync();

		Task RefreshAsync(bool refreshLayout);

		Node Root { get; }

		void ShowHiddenNode(NodeName nodeName);

		IReadOnlyList<NodeName> GetHiddenNodeNames();

		void Close();
	}
}