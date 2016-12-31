using System.Windows;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.MainViews
{
	public interface ICanvasService
	{
		bool ZoomCanvas(ZoomableCanvas canvas, int zoomDelta, Point viewPosition);

		bool MoveCanvas(ZoomableCanvas canvas, Vector viewOffset);
		Point GetCanvasPosition(ZoomableCanvas canvas, Point viewPosition);
	}
}