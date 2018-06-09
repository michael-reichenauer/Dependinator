using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Dependinator.Common;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing
{
	internal interface IModelViewService
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