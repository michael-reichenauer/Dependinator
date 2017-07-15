using System.Threading.Tasks;
using System.Windows;
using Dependinator.ModelViewing.Private.Items;


namespace Dependinator.ModelViewing.Private
{
	internal interface IModelViewService
	{
		Task LoadAsync(IItemsCanvas rootCanvas);

		void Zoom(double zoomFactor, Point zoomCenter);
		void Move(Vector viewOffset);

		void Close();
		Task Refresh(IItemsCanvas rootCanvas, bool refreshLayout);
	}
}