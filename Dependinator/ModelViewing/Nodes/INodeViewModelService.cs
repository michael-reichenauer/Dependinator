using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Dependinator.ModelHandling;
using Dependinator.ModelHandling.Core;
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
		bool MovePoint(Node node, int index, Point point, Point previousPoint);
		Brush GetNodeBrush(Node node);
		void FirstShowNode(Node node);


		IEnumerable<LinkItem> GetIncomingLinkItems(Node node);
		IEnumerable<LinkItem> GetOutgoingLinkItems(Node node);
	}
}