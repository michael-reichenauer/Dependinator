using System.Windows;
using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.Lines.Private
{
	internal interface ILineControlService
	{
		bool IsOnLineBetweenNeighbors(Line line, int index);
		void MoveLinePoint(Line line, int pointIndex, Point newPoint, double scale);
		void RemovePoint(Line line);
		int GetLinePointIndex(Line line, Point point, bool isPointMove);
		void UpdateLineBounds(Line line);
	}
}