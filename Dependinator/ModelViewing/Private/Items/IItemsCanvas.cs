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
		Point Offset { get; }
		double Scale { get; }
		double ParentScale { get; }
		Point ChildToParentCanvasPoint(Point childCanvasPoint);
		Point ParentToChildCanvasPoint(Point parentCanvasPoint);
		void SetZoomableCanvas(ZoomableCanvas canvas);
		void AddItem(ItemViewModel item);
		void AddItems(IEnumerable<ItemViewModel> items);
		void RemoveItem(ItemViewModel item);
		void RemoveAll();
		void UpdateItem(ItemViewModel item);
		void UpdateItems(IEnumerable<ItemViewModel> items);
		void Zoom(double zoom, Point? zoomCenter = null);
		void Move(Vector viewOffset);
		void SizeChanged();
		void TriggerInvalidated();
		Rect GetHierarchicalVisualArea();
		IItemsCanvas CreateChild(IItemsCanvasBounds canvasBounds);
		void SetInitialScale(double scale);
	}
}