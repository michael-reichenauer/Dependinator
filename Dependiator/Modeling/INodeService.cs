using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;


namespace Dependiator.Modeling
{
	internal interface INodeService
	{
		double Scale { get; set; }

		Rect CurrentViewPort { get; }

		void ShowNodes(IEnumerable<Node> nodes);

		void HideNodes(IEnumerable<Node> nodes);

		void ShowNode(Node node);

		void HideNode(Node node);

		Brush GetRectangleBrush();

		void RemoveRootNode(Node node);
		void AddRootNode(Node node);

		object MoveNode(Point viewPosition, Vector viewOffset, object movingObject);
		void UpdateNode(Node node);
		Brush GetRectangleBackgroundBrush(Brush brush);
	}
}