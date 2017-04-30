using System.Collections.Generic;


namespace Dependiator.Modeling.Links
{
	internal interface ILinkService
	{
		LinkLineBounds GetLinkLineBounds(LinkLine line);
		IReadOnlyList<LinkGroup> GetLinkGroups(LinkLine line);
		double GetLineThickness(LinkLine linkLine);
		IReadOnlyList<LinkLine> GetLinkSegments(Link link);
	}
}