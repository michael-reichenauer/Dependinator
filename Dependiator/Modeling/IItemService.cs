using System.Windows.Media;


namespace Dependiator.Modeling
{
	internal interface IItemService
	{
		Brush GetRandomRectangleBrush();
		Brush GetRectangleBackgroundBrush(Brush brush);
		Brush GetBrushFromHex(string hexColor);
		string GetHexColorFromBrush(Brush brush);
		void SetChildrenLayout(Node parent);
	}
}