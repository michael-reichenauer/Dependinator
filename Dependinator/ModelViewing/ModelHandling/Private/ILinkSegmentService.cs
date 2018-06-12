using System.Collections.Generic;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal interface ILinkSegmentService
	{
		IReadOnlyList<LinkSegment> GetLinkSegments(Link link);
	}
}