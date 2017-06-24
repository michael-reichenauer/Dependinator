using System.Windows.Media;


namespace Dependinator.Modeling.Nodes
{
	internal interface INodeService
	{
		Brush GetRandomRectangleBrush();
		Brush GetRectangleBackgroundBrush(Brush brush);
		Brush GetBrushFromHex(string hexColor);
		string GetHexColorFromBrush(Brush brush);
		void SetChildrenLayout(Node parent);
		Brush GetRectangleHighlightBrush(Brush brush);
	}
}