using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;


namespace Dependiator.Modeling
{
	internal interface INodeService
	{
		double Scale { get; }

		void ShowNodes(IEnumerable<Node> nodes);

		void HideNodes(IEnumerable<Node> nodes);

		void ShowNode(Node node);

		void HideNode(Node node);

		Brush GetNextBrush();

		void RemoveRootNode(Node node);
		void AddRootNode(Node node);

		object MoveNode(Point viewPosition, Vector viewOffset, object movingObject);
		void UpdateNode(Node node);
	}
}