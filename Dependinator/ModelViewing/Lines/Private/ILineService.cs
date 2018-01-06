using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.Lines.Private
{
	internal interface ILineService
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