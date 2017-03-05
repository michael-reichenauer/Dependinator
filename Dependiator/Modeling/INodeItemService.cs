using System.Windows.Media;


namespace Dependiator.Modeling
{
	internal interface INodeItemService
	{
		Brush GetRandomRectangleBrush();
		Brush GetRectangleBackgroundBrush(Brush brush);
		void AddModuleChildren(Node parent, NodesViewModel viewModel);
		Brush GetBrushFromHex(string hexColor);
		string GetHexColorFromBrush(Brush brush);
	}
}