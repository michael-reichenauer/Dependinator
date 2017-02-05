using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;


namespace Dependiator.Modeling
{
	internal interface IItemService
	{
		double CanvasScale { get; set; }

		Rect CurrentViewPort { get; }

		void ShowItems(IEnumerable<Item> nodes);

		void HideItems(IEnumerable<Item> nodes);

		void ShowItem(Item item);

		void HideItem(Item item);

		Brush GetRectangleBrush();

		void RemoveRootNode(Item item);
		void AddRootItem(Item item);

		object MoveItem(Point viewPosition, Vector viewOffset, object movingObject);
		void UpdateItem(Item item);
		Brush GetRectangleBackgroundBrush(Brush brush);
		void ShowRootItem(Item item);
		void ClearAll();
	}
}