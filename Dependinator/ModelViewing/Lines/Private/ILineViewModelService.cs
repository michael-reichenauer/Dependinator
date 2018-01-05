using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.Lines.Private
{
	internal interface ILineViewModelService
	{
		double GetLineWidth(Line line);
		string GetLineData(Line line);
		string GetPointsData(Line line);
		string GetArrowData(Line line);
		double GetArrowWidth(Line line);
		IEnumerable<LineMenuItemViewModel> GetSourceLinkItems(Line line);
		IEnumerable<LineMenuItemViewModel> GetTargetLinkItems(Line line);
		void OnMouseWheel(LineViewModel lineViewModel, UIElement uiElement, MouseWheelEventArgs e);
		string GetEndPointsData(Line line);
		void Clicked(LineViewModel lineViewModel);
		void UpdateLineBounds(Line line);
		void UpdateLineEndPoints(Line line);
		LineControl GetLineControl(Line line);
	}
}