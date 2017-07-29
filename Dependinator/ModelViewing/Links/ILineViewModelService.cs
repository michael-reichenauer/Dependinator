using System.Collections.Generic;
using Dependinator.ModelViewing.Links.Private;
using Dependinator.ModelViewing.Nodes;


namespace Dependinator.ModelViewing.Links
{
	internal interface ILineViewModelService
	{
		LinkLineBounds GetLinkLineBounds(LinkLineOld line);
		IReadOnlyList<LinkGroup> GetLinkGroups(LinkLineOld line);
		double GetLineThickness(LinkLineOld linkLine);
	
		void AddLinkLines(LinkOld link);
		//void ZoomInLinkLine(LinkLine linkLine);
		void ZoomInLinkLine(LinkLineOld line, NodeOld node);
		//void ZoomOutLinkLine(LinkLine linkLine);
		void ZoomOutLinkLine(LinkLineOld line, NodeOld node);
		void CloseLine(LinkLineOld linkLine);
	}
}