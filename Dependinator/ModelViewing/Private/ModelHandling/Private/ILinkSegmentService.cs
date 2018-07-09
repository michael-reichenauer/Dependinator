using System.Collections.Generic;
using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.ModelHandling.Private
{
	internal interface ILinkSegmentService
	{
		IEnumerable<LinkSegment> GetLinkSegments(Link link);
	}
}