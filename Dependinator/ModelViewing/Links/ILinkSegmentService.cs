using System.Collections.Generic;
using Dependinator.ModelViewing.Links.Private;
using Dependinator.ModelViewing.Nodes;


namespace Dependinator.ModelViewing.Links
{
	internal interface ILinkSegmentService
	{
		IReadOnlyList<LinkSegment> GetNewLinkSegments(
			IReadOnlyList<LinkSegment> linkSegments, LinkSegment newSegment);


		IReadOnlyList<LinkSegment> GetNewLinkSegments(
			IReadOnlyList<LinkSegment> linkSegments,
			IReadOnlyList<LinkSegment> newSegments);


		LinkSegment GetZoomedInSegment(
			IReadOnlyList<LinkSegment> replacedSegments, Link link);


		IReadOnlyList<LinkSegment> GetZoomedInReplacedSegments(
			IEnumerable<LinkSegment> linkSegments,
			Node source,
			Node target);


		IReadOnlyList<LinkSegment> GetZoomedOutReplacedSegments(
			IReadOnlyList<LinkSegment> normalSegments,
			IReadOnlyList<LinkSegment> currentSegments,
			Node source,
			Node target);


		IReadOnlyList<LinkSegment> GetNormalLinkSegments(Link link);


		IReadOnlyList<LinkSegment> GetZoomedInBeforeReplacedSegments(
			IEnumerable<LinkSegment> linkSegments,
			Node source,
			Node target);


		IReadOnlyList<LinkSegment> GetZoomedInAfterReplacedSegments(
			IEnumerable<LinkSegment> linkSegments,
			Node source,
			Node target);
	}
}