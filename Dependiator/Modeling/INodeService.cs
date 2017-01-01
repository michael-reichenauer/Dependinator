using System.Collections.Generic;
using System.Windows.Media;


namespace Dependiator.Modeling
{
	internal interface INodeService
	{
		double Scale { get; }

		void ShowNode(Node node);

		void HideNode(Node node);
		void ShowNodes(IEnumerable<Node> nodes);
		Brush GetNextBrush();

		void RemoveRootNode(Node node);
		void AddRootNode(Node node);
	}
}