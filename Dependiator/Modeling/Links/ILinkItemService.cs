using System.Collections.Generic;


namespace Dependiator.Modeling.Links
{
	internal interface ILinkItemService
	{
		LinkSegmentLine GetLinkSegmentLine(LinkSegment segment);
		IReadOnlyList<LinkGroup> GetLinkGroups(LinkSegment segment);
	}
}