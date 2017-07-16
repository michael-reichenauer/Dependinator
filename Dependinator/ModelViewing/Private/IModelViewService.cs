using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.Items;


namespace Dependinator.ModelViewing.Private
{
	internal interface IModelViewService
	{
		Task LoadAsync(IItemsCanvas rootCanvas);


		void Close();
		Task Refresh(IItemsCanvas rootCanvas, bool refreshLayout);
	}
}