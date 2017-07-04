using System.Threading.Tasks;
using System.Windows;
using Dependinator.ModelViewing.Items;


namespace Dependinator.ModelViewing
{
	internal interface IRootModelService
	{
		void InitModules(ItemsCanvas rootCanvas);

		void Zoom(double zoomFactor, Point zoomCenter);
		void Move(Vector viewOffset);

		void Close();
		Task Refresh(ItemsCanvas rootCanvas, bool refreshLayout);
	}
}