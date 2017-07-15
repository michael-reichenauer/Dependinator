using System.Threading.Tasks;
using System.Windows;
using Dependinator.ModelViewing.Private.Items;

namespace Dependinator.ModelViewing.Private
{
	internal interface IModelService
	{
		void Init(IItemsCanvas rootCanvas);
		Task RefreshAsync(bool refreshLayout);
		void ZoomRoot(double zoomFactor, Point zoomCenter);
		void MoveRootItems(Vector viewOffset);
	}
}