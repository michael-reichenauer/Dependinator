using System.Threading.Tasks;
using System.Windows;


namespace Dependiator.Modeling
{
	internal interface IModelService
	{
		void InitModules();
		object MoveNode(Point viewPosition, Vector viewOffset, object movingObject);
		bool ZoomNode(int zoomDelta, Point viewPosition);

		bool ZoomRoot(double scale);

		void Close();
		Task Refresh();
	}
}