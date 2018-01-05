using System.Windows;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.Links.Private
{
	internal interface ILineControlService
	{
		bool IsOnLineBetweenNeighbors(Line line, int index);
		void MoveLinePoint(Line line, int pointIndex, Point newPoint);
		void RemovePoint(Line line);
		int GetLinePointIndex(Line line, Point point, bool isPointMove);
		void UpdateLineBounds(Line line);
	}
}