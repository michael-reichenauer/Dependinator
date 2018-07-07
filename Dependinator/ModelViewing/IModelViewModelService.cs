using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.Nodes;


namespace Dependinator.ModelViewing
{
	internal interface IModelViewModelService
	{
		void SetRootCanvas(ItemsCanvas rootCanvas);

		Task RefreshAsync(bool refreshLayout);

		void ShowHiddenNode(NodeName nodeName);

		void Close();
		IReadOnlyList<NodeName> GetHiddenNodeNames();
		void Clicked();
		void OnMouseWheel(UIElement uiElement, MouseWheelEventArgs e);
	}
}