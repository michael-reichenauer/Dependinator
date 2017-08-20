using System.Threading.Tasks;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Private.Items;

namespace Dependinator.ModelViewing.Private
{
	internal interface IModelService
	{
		void Init(ItemsCanvas rootCanvas);
		Task RefreshAsync(bool refreshLayout);

		Node Root { get; }

		Task LoadAsync();
		void ClearAll();
		Task SaveAsync();
		void Save();
	}
}