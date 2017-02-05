using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;


namespace Dependiator.Modeling
{
	internal interface INodeService
	{
		double Scale { get; set; }

		Rect CurrentViewPort { get; }

		void ShowNodes(IEnumerable<Item> nodes);

		void HideNodes(IEnumerable<Item> nodes);

		void ShowNode(Item item);

		void HideNode(Item item);

		Brush GetRectangleBrush();

		void RemoveRootNode(Item item);
		void AddRootNode(Item item);

		object MoveNode(Point viewPosition, Vector viewOffset, object movingObject);
		void UpdateNode(Item item);
		Brush GetRectangleBackgroundBrush(Brush brush);
		void ShowRootNode(Item item);
		void ClearAll();
	}
}