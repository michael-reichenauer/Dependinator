using System.Collections.Generic;

namespace Dependinator.ModelViewing.Links.Private
{
	internal interface ILinkSegmentService
	{
		IReadOnlyList<LinkSegment> GetLinkSegments(Link link);

		//IReadOnlyList<LinkSegmentOld> GetNewLinkSegments(
		//	IReadOnlyList<LinkSegmentOld> linkSegments, LinkSegmentOld newSegment);


		//IReadOnlyList<LinkSegmentOld> GetNewLinkSegments(
		//	IReadOnlyList<LinkSegmentOld> linkSegments,
		//	IReadOnlyList<LinkSegmentOld> newSegments);


		//LinkSegmentOld GetZoomedInSegment(
		//	IReadOnlyList<LinkSegmentOld> replacedSegments, LinkOld link);


		//IReadOnlyList<LinkSegmentOld> GetZoomedInReplacedSegments(
		//	IEnumerable<LinkSegmentOld> linkSegments,
		//	NodeOld source,
		//	NodeOld target);


		//IReadOnlyList<LinkSegmentOld> GetZoomedOutReplacedSegments(
		//	IReadOnlyList<LinkSegmentOld> normalSegments,
		//	IReadOnlyList<LinkSegmentOld> currentSegments,
		//	NodeOld source,
		//	NodeOld target);





		//IReadOnlyList<LinkSegmentOld> GetZoomedInBeforeReplacedSegments(
		//	IEnumerable<LinkSegmentOld> linkSegments,
		//	NodeOld source,
		//	NodeOld target);


		//IReadOnlyList<LinkSegmentOld> GetZoomedInAfterReplacedSegments(
		//	IEnumerable<LinkSegmentOld> linkSegments,
		//	NodeOld source,
		//	NodeOld target);
	}
}