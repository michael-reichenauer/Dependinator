using System.Threading.Tasks;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.ModelViewing.Private.Items.Private;

namespace Dependinator.ModelViewing.Private
{
	internal interface IModelService
	{
		void Init(ItemsCanvas rootCanvas);
		Task RefreshAsync(bool refreshLayout);

		Task LoadAsync();
		Node Root { get; }
	}
}