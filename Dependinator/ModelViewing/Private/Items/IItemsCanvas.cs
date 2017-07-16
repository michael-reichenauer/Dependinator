using System.Collections.Generic;
using System.Windows;
using Dependinator.Utils.UI.VirtualCanvas;

namespace Dependinator.ModelViewing.Private.Items
{
	internal interface IItemsCanvas
	{
		bool IsRoot { get; }
		IItemsCanvas CanvasRoot { get; }
		double ScaleFactor { get; }
		Point Offset { get; set; }
		double Scale { get; set; }
		double ParentScale { get; }
		void UpdateScale();
		Point ChildToParentCanvasPoint(Point childCanvasPoint);
		Point ParentToChildCanvasPoint(Point parentCanvasPoint);
		void SetZoomableCanvas(ZoomableCanvas canvas);
		void AddItem(IItem item);
		void AddItems(IEnumerable<IItem> items);
		void RemoveItem(IItem item);
		void RemoveAll();
		void UpdateItem(IItem item);
		void UpdateItems(IEnumerable<IItem> items);
		void Zoom(double zoom, Point? zoomCenter = null);
		void Move(Vector viewOffset);
		void SizeChanged();
		void TriggerInvalidated();
		Rect GetHierarchicalVisualArea();
		IItemsCanvas CreateChild(IItemsCanvasBounds canvasBounds);
	}
}