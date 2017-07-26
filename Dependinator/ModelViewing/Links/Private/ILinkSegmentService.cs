using System.Collections.Generic;
using Dependinator.ModelViewing.Nodes;

namespace Dependinator.ModelViewing.Links.Private
{
	internal interface ILinkSegmentService
	{
		IReadOnlyList<LinkSegmentOld> GetNewLinkSegments(
			IReadOnlyList<LinkSegmentOld> linkSegments, LinkSegmentOld newSegment);


		IReadOnlyList<LinkSegmentOld> GetNewLinkSegments(
			IReadOnlyList<LinkSegmentOld> linkSegments,
			IReadOnlyList<LinkSegmentOld> newSegments);


		LinkSegmentOld GetZoomedInSegment(
			IReadOnlyList<LinkSegmentOld> replacedSegments, LinkOld link);


		IReadOnlyList<LinkSegmentOld> GetZoomedInReplacedSegments(
			IEnumerable<LinkSegmentOld> linkSegments,
			NodeOld source,
			NodeOld target);


		IReadOnlyList<LinkSegmentOld> GetZoomedOutReplacedSegments(
			IReadOnlyList<LinkSegmentOld> normalSegments,
			IReadOnlyList<LinkSegmentOld> currentSegments,
			NodeOld source,
			NodeOld target);


		IReadOnlyList<LinkSegmentOld> GetNormalLinkSegments(LinkOld link);


		IReadOnlyList<LinkSegmentOld> GetZoomedInBeforeReplacedSegments(
			IEnumerable<LinkSegmentOld> linkSegments,
			NodeOld source,
			NodeOld target);


		IReadOnlyList<LinkSegmentOld> GetZoomedInAfterReplacedSegments(
			IEnumerable<LinkSegmentOld> linkSegments,
			NodeOld source,
			NodeOld target);
	}
}