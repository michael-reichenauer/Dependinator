using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Dependinator.ModelViewing.Private.ItemsViewing;


namespace Dependinator.ModelViewing.Private
{
	internal interface IModelViewModelService
	{
		void SetRootCanvas(ItemsCanvas rootCanvas);

		Task RefreshAsync(bool refreshLayout);

		void ShowHiddenNode(NodeName nodeName);

		Task CloseAsync();
		IReadOnlyList<NodeName> GetHiddenNodeNames();
		void Clicked();
		void OnMouseWheel(UIElement uiElement, MouseWheelEventArgs e);
		Task OpenAsync();
		Task OpenFilesAsync(IReadOnlyList<string> filePaths);
		void AddNewNode();
	}
}
