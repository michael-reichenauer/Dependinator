using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.Items;


namespace Dependinator.ModelViewing.Private
{
	internal interface IModelViewService
	{
		void SetRootCanvas(ItemsCanvas rootCanvas);

		Task LoadAsync();

		Task RefreshAsync(bool refreshLayout);

		void Close();
	}
}