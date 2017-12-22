using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.Common;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ModelHandling
{
	internal interface IModelService
	{
		void SetRootCanvas(ItemsCanvas rootCanvas);
		Task RefreshAsync(bool refreshLayout);

		Node Root { get; }

		void AddLineViewModel(Line line);

		Task LoadAsync();
		void ShowHiddenNode(NodeName nodeName);
		void ClearAll();
		Task SaveAsync();
		void Save();
		IReadOnlyList<NodeName> GetHiddenNodeNames();
	}
}