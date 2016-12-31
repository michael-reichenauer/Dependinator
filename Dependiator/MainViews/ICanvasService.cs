using System.Windows;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.MainViews
{
	public interface ICanvasService
	{
		bool HandleZoom(ZoomableCanvas canvas, int zoomDelta, Point currentPosition);
	}
}