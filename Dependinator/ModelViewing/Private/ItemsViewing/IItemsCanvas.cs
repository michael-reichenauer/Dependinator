using System.Windows;
using System.Windows.Input;
using Dependinator.Utils.UI.VirtualCanvas;


namespace Dependinator.ModelViewing.Private.ItemsViewing
{
	internal interface IItemsCanvas
	{
		bool IsFocused { get; set; }
		ZoomableCanvas ZoomableCanvas { get; }
		ItemsCanvas ParentCanvas { get; }
		Rect ItemsCanvasBounds { get; }
		bool IsZoomAndMoveEnabled { get; set; }
		ItemsCanvas RootCanvas { get; }
		bool IsRoot { get; }
		double ScaleFactor { get; set; }
		double Scale { get; }
		Point RootOffset { get; }
		void SetRootOffset(Point offset);
		void SetRootScale(double scale);
		void AddItem(IItem item);
		void RemoveItem(IItem item);
		void RemoveAll();
		void UpdateItem(IItem item);
		void SizeChanged();
		void RemoveChildCanvas(ItemsCanvas childCanvas);
		void CanvasRealized();
		void CanvasVirtualized();
		void UpdateAll();
		void Zoom(MouseWheelEventArgs e);
		void ZoomRoot(double zoom);
		//void ZoomNode(double zoom, Point? zoomCenter);
		void UpdateAndNotifyAll(bool isUpdate);
		bool IsNodeInViewBox(Rect bounds);
		void MoveAllItems(Point sp1, Point sp2);
		void MoveAllItems(Vector viewOffset);
		void UpdateScale();
		Point MouseToCanvasPoint();
		Point MouseEventToCanvasPoint(MouseEventArgs e);
		Point CanvasToScreenPoint(Point canvasPoint);
		Point ScreenToCanvasPoint(Point screenPoint);
		Point ParentToChildCanvasPoint(Point parentCanvasPoint);
		Point ChildToParentCanvasPoint(Point childCanvasPoint);
		Point CanvasToScreenPoint2(Point childCanvasPoint);
		void SetZoomableCanvas(ZoomableCanvas canvas);
		string ToString();
		int ChildItemsCount();
		int ShownChildItemsCount();
		int DescendantsItemsCount();
		int ShownDescendantsItemsCount();
	}
}