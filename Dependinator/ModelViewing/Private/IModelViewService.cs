using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.Items;


namespace Dependinator.ModelViewing.Private
{
	internal interface IModelViewService
	{
		Task LoadAsync(ItemsCanvas rootCanvas);

		Task CloseAsync();

		Task Refresh(ItemsCanvas rootCanvas, bool refreshLayout);
	}
}