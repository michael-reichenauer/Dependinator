using System.Collections.Generic;


namespace Dependiator.Modeling.Links
{
	internal interface ILinkService
	{
		LinkLineBounds GetLinkSegmentLine(LinkSegment segment);
		IReadOnlyList<LinkGroup> GetLinkGroups(LinkSegment segment);
		double GetLineThickness(LinkSegment linkSegment);
		IReadOnlyList<LinkSegment> GetLinkSegments(Link link);
	}
}