using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Dependinator.ModelViewing.Links.Private;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.Links
{
	internal interface ILineViewModelService
	{
		//void ZoomInLinkLine(LinkLine linkLine);
		//void ZoomOutLinkLine(LinkLine linkLine);

		//bool IsOnLineBetweenNeighbors(Line points, int index);
		//int GetLinePointIndex(Line line, Point point, bool isPointMove);
		//void AddLinkLines(LinkOld link);
		//void ZoomInLinkLine(LinkLineOld line, NodeOld node);
		//void ZoomOutLinkLine(LinkLineOld line, NodeOld node);
		//void CloseLine(LinkLineOld line);
		//double GetLineThickness(LinkLineOld linkLine);
		//LinkLineBounds GetLinkLineBounds(LinkLineOld line);

		/// <summary>
		/// Gets the links in the line grouped first by source and then by target at the
		/// appropriate node levels.
		/// </summary>
		IReadOnlyList<LinkGroup> GetLinkGroups(Line line);

		string GetLineToolTip(Line line);
		double GetLineWidth(Line line);
		string GetLineData(Line line);
		string GetPointsData(Line line);
		string GetArrowData(Line line);
		//void MoveLinePoint(Line line, int pointIndex, Point point);
		//void UpdateLineEndPoints(Line line);
		//void UpdateLineBounds(Line line);
		double GetArrowWidth(Line line);
		IEnumerable<LinkItem> GetSourceLinkItems(Line line);
		IEnumerable<LinkItem> GetTargetLinkItems(Line line);
		void OnMouseWheel(LineViewModel lineViewModel, UIElement uiElement, MouseWheelEventArgs e);
		string GetEndPointsData(Line line);
		void Clicked(LineViewModel lineViewModel);
		//void RemovePoint(Line line);
		void UpdateLineBounds(Line line);
		void UpdateLineEndPoints(Line line);
	}
}