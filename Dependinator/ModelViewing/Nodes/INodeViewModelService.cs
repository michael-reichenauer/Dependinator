using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Dependinator.ModelViewing.Links;


namespace Dependinator.ModelViewing.Nodes
{
	internal interface INodeViewModelService
	{
		Brush GetRandomRectangleBrush(string nodeName);
		Brush GetBackgroundBrush(Brush brush);
		Brush GetBrushFromHex(string hexColor);
		string GetHexColorFromBrush(Brush brush);
		Brush GetRectangleHighlightBrush(Brush brush);

		int GetPointIndex(Node node, Point point);
		void MovePoint(Node node, int index, Point point, Point previousPoint);
		Brush GetNodeBrush(Node node);
		void FirstShowNode(Node node);


		IEnumerable<LinkItem> GetLinkItems(
			IEnumerable<Line> links, Func<Line, Node> lineEndPoint, Func<Link, Node> endPoint);
	}
}