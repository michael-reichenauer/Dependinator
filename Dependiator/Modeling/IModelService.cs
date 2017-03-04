using System.Threading.Tasks;
using System.Windows;
using Dependiator.MainViews.Private;
using Dependiator.Modeling.Items;


namespace Dependiator.Modeling
{
	internal interface IModelService
	{
		void InitModules(ItemsCanvas itemsCanvas);
		object MoveNode(Point viewPosition, Vector viewOffset, object movingObject);
		bool ZoomNode(int zoomDelta, Point viewPosition);

		bool ZoomRoot(double scale);

		void Close();
		Task Refresh(ItemsCanvas itemsCanvas);
	}
}