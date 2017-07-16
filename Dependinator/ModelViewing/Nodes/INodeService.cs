using System.Windows.Media;


namespace Dependinator.ModelViewing.Nodes
{
	internal interface INodeService
	{
		Brush GetRandomRectangleBrush();
		Brush GetRectangleBackgroundBrush(Brush brush);
		Brush GetBrushFromHex(string hexColor);
		string GetHexColorFromBrush(Brush brush);
		void SetChildrenLayout(NodeOld parent);
		Brush GetRectangleHighlightBrush(Brush brush);
		void SetLayout(NodeViewModel nodeViewModel);
	}
}