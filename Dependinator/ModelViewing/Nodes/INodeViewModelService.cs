using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.ModelViewing.Lines.Private;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.Nodes
{
	internal interface INodeViewModelService
	{
		Brush GetRandomRectangleBrush(string nodeName);
		Brush GetBackgroundBrush(Brush brush);
		Brush GetBrushFromHex(string hexColor);
		string GetHexColorFromBrush(Brush brush);
		Brush GetRectangleHighlightBrush(Brush brush);
		
		Brush GetNodeBrush(Node node);
		void FirstShowNode(Node node);
		
		IEnumerable<LineMenuItemViewModel> GetIncomingLinkItems(Node node);
		IEnumerable<LineMenuItemViewModel> GetOutgoingLinkItems(Node node);
		void MouseClicked(NodeViewModel nodeViewModel);
		void OnMouseWheel(NodeViewModel nodeViewModel, UIElement uiElement, MouseWheelEventArgs e);
		Brush GetSelectedBrush(Brush brush);
		void ShowReferences(NodeViewModel nodeViewModel, bool isIncoming);
		void ShowCode(Node node);
	}
}