using System.Windows;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.MainViews
{
	public interface ICanvasService
	{
		bool ZoomCanvas(int zoomDelta, Point viewPosition);

		bool MoveCanvas(Vector viewOffset);

		Point GetCanvasPosition(Point viewPosition);

		void SetCanvas(ZoomableCanvas zoomableCanvas);

		double Scale { get; }
	}
}