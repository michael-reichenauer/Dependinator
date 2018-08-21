using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.Lines.Private
{
    internal interface ILineDataService
    {
        void UpdateLineEndPoints(Line line);
        void UpdateLineBounds(Line line);
        string GetLineData(Line line);
        string GetPointsData(Line line);
        string GetEndPointsData(Line line);
        string GetArrowData(Line line);
        double GetLineWidth(Line line);
        double GetArrowWidth(Line line);
    }
}
