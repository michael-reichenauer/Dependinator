using System.Windows;
using System.Windows.Media;


namespace Dependinator.ModelViewing.Nodes
{
	internal interface INodeViewModelService
	{
		Brush GetRandomRectangleBrush();
		Brush GetRectangleBackgroundBrush(Brush brush);
		Brush GetBrushFromHex(string hexColor);
		string GetHexColorFromBrush(Brush brush);
		Brush GetRectangleHighlightBrush(Brush brush);
		void SetLayout(NodeViewModel node);
		void ResetLayout(NodeViewModel nodeViewMode);
		int GetPointIndex(Node node, Point point);
		void MovePoint(Node node, int index, Point point, Point previousPoint);
	}
}