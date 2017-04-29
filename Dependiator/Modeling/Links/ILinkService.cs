using System.Collections.Generic;


namespace Dependiator.Modeling.Links
{
	internal interface ILinkService
	{
		LinkSegmentLine GetLinkSegmentLine(LinkSegment segment);
		IReadOnlyList<LinkGroup> GetLinkGroups(LinkSegment segment);
		double GetLineThickness(LinkSegment linkSegment);
	}
}