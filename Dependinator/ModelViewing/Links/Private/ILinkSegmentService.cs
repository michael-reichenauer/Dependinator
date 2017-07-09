using System.Collections.Generic;
using Dependinator.ModelViewing.Nodes;

namespace Dependinator.ModelViewing.Links.Private
{
	internal interface ILinkSegmentService
	{
		IReadOnlyList<LinkSegment> GetNewLinkSegments(
			IReadOnlyList<LinkSegment> linkSegments, LinkSegment newSegment);


		IReadOnlyList<LinkSegment> GetNewLinkSegments(
			IReadOnlyList<LinkSegment> linkSegments,
			IReadOnlyList<LinkSegment> newSegments);


		LinkSegment GetZoomedInSegment(
			IReadOnlyList<LinkSegment> replacedSegments, LinkOld link);


		IReadOnlyList<LinkSegment> GetZoomedInReplacedSegments(
			IEnumerable<LinkSegment> linkSegments,
			NodeOld source,
			NodeOld target);


		IReadOnlyList<LinkSegment> GetZoomedOutReplacedSegments(
			IReadOnlyList<LinkSegment> normalSegments,
			IReadOnlyList<LinkSegment> currentSegments,
			NodeOld source,
			NodeOld target);


		IReadOnlyList<LinkSegment> GetNormalLinkSegments(LinkOld link);


		IReadOnlyList<LinkSegment> GetZoomedInBeforeReplacedSegments(
			IEnumerable<LinkSegment> linkSegments,
			NodeOld source,
			NodeOld target);


		IReadOnlyList<LinkSegment> GetZoomedInAfterReplacedSegments(
			IEnumerable<LinkSegment> linkSegments,
			NodeOld source,
			NodeOld target);
	}
}