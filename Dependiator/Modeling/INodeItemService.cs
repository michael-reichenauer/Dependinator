using System.Windows.Media;


namespace Dependiator.Modeling
{
	internal interface INodeItemService
	{
		Brush GetRandomRectangleBrush();
		Brush GetRectangleBackgroundBrush(Brush brush);
		void SetChildrenItemBounds(Node parent);
		Brush GetBrushFromHex(string hexColor);
		string GetHexColorFromBrush(Brush brush);
	}
}