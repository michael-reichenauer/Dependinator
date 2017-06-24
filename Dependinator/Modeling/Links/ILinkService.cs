using System.Collections.Generic;
using Dependinator.Modeling.Nodes;


namespace Dependinator.Modeling.Links
{
	internal interface ILinkService
	{
		LinkLineBounds GetLinkLineBounds(LinkLine line);
		IReadOnlyList<LinkGroup> GetLinkGroups(LinkLine line);
		double GetLineThickness(LinkLine linkLine);
	
		void AddLinkLines(Link link);
		//void ZoomInLinkLine(LinkLine linkLine);
		void ZoomInLinkLine(LinkLine line, Node node);
		//void ZoomOutLinkLine(LinkLine linkLine);
		void ZoomOutLinkLine(LinkLine line, Node node);
		void CloseLine(LinkLine linkLine);
	}
}