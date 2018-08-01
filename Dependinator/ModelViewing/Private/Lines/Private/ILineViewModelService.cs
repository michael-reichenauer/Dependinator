using System.Windows;
using System.Windows.Input;
using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.Lines.Private
{
    internal interface ILineViewModelService
    {
        double GetLineWidth(Line line);
        string GetLineData(Line line);
        string GetPointsData(Line line);
        string GetArrowData(Line line);
        double GetArrowWidth(Line line);
        void OnMouseWheel(LineViewModel lineViewModel, UIElement uiElement, MouseWheelEventArgs e);
        string GetEndPointsData(Line line);
        void Clicked(LineViewModel lineViewModel);
        void UpdateLineBounds(Line line);
        void UpdateLineEndPoints(Line line);
        LineControl GetLineControl(Line line);
        void ShowReferences(LineViewModel lineViewModel);
        void SetIsChanged(LineViewModel lineViewModel);
    }
}
