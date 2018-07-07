using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.Nodes
{
	internal interface INodeViewModelService
	{
		Brush GetBackgroundBrush(Brush brush);

		Brush GetNodeBrush(Node node);
		void FirstShowNode(Node node);

		void MouseClicked(NodeViewModel nodeViewModel);
		void OnMouseWheel(NodeViewModel nodeViewModel, UIElement uiElement, MouseWheelEventArgs e);
		Brush GetSelectedBrush(Brush brush);
		void ShowReferences(NodeViewModel nodeViewModel);
		void ShowCode(Node node);
		void RearrangeLayout(NodeViewModel node);
		void HideNode(Node node);
		Brush GetDimBrush();
		Brush GetTitleBrush();
		void ShowNode(Node node);
		void SetIsChanged();
	}
}