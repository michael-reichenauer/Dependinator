using System.Threading.Tasks;
using System.Windows;
using Dependiator.Modeling.Items;


namespace Dependiator.MainViews.Private
{
	internal interface IModelViewService
	{
		void InitModules(ItemsCanvas rootCanvas);

		void Zoom(double zoomFactor, Point zoomCenter);
		void Move(Vector viewOffset);

		void Close();
		Task Refresh(ItemsCanvas rootCanvas, bool refreshLayout);
	}
}