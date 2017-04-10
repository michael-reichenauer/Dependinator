using System.Threading.Tasks;
using System.Windows;
using Dependiator.Modeling.Items;


namespace Dependiator.Modeling
{
	internal interface IModelService
	{
		void InitModules(ItemsCanvas rootCanvas);
		object MoveNode(Point viewPosition, Vector viewOffset, object movingObject);
		bool ZoomNode(int zoomDelta, Point viewPosition);

		void Zoom(double zoomFactor, Point zoomCenter);
		void Move(Vector viewOffset);

		void Close();
		Task Refresh(ItemsCanvas rootCanvas);
	}
}