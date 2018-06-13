using System.Collections.Generic;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal interface ILinkSegmentService
	{
		IEnumerable<LinkSegment> GetLinkSegments(Link link);
	}
}