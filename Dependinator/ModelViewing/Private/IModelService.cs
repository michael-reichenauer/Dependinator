using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.Items;

namespace Dependinator.ModelViewing.Private
{
	internal interface IModelService
	{
		void Init(IItemsCanvas rootCanvas);
		Task RefreshAsync(bool refreshLayout);

		Task LoadAsync();
	}
}