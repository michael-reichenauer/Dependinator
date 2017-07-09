using System.Collections.Generic;
using Dependinator.ModelViewing.Links.Private;
using Dependinator.ModelViewing.Nodes;


namespace Dependinator.ModelViewing.Links
{
	internal interface ILinkService
	{
		LinkLineBounds GetLinkLineBounds(LinkLine line);
		IReadOnlyList<LinkGroup> GetLinkGroups(LinkLine line);
		double GetLineThickness(LinkLine linkLine);
	
		void AddLinkLines(LinkOld link);
		//void ZoomInLinkLine(LinkLine linkLine);
		void ZoomInLinkLine(LinkLine line, NodeOld node);
		//void ZoomOutLinkLine(LinkLine linkLine);
		void ZoomOutLinkLine(LinkLine line, NodeOld node);
		void CloseLine(LinkLine linkLine);
	}
}