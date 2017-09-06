using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.Items;


namespace Dependinator.ModelViewing.Private
{
	internal interface IModelViewService
	{
		void Init(ItemsCanvas rootCanvas);

		Task LoadAsync();

		Task Refresh(bool refreshLayout);

		void Close();
	}
}