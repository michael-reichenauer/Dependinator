using System.Windows.Media;


namespace Dependiator.Modeling
{
	internal interface INodeItemService
	{
		Brush GetRectangleBrush();
		Brush GetRectangleBackgroundBrush(Brush brush);
		void AddModuleChildren(Node parent, NodesViewModel viewModel);
	}
}