using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal class LinkSegmentService : ILinkSegmentService
	{
		public IReadOnlyList<LinkSegment> GetLinkSegments(Link link)
		{
			if (link.Source.Parent == link.Target.Parent ||
			    link.Source == link.Target.Parent ||
			    link.Source.Parent == link.Target)
			{
				// Sibling, parent or child link
				return new[] { new LinkSegment(link.Source, link.Target) };
			}

			List<LinkSegment> segments = new List<LinkSegment>();
			List<Node> sourceAncestors = SourceAncestorsAndSel(link).Reverse().ToList();
			List<Node> targetAncestors = TargetAncestorsAndSelf(link).Reverse().ToList();
			
			for (int i = 0; i < sourceAncestors.Count; i++)
			{
				if (sourceAncestors[i] != targetAncestors[i])
				{
					// Cousins (nodes within siblings)
					Node segmentSource = link.Source;

					// From source going upp to common ancestor
					for (int j = sourceAncestors.Count - 2; j >= i; j--)
					{
						Node segmentTarget = sourceAncestors[j];
						segments.Add(new LinkSegment(segmentSource, segmentTarget));
						segmentSource = segmentTarget;
					}

					// From common ancestor going down to target
					for (int j = i; j < targetAncestors.Count; j++)
					{
						Node segmentTarget = targetAncestors[j];
						segments.Add(new LinkSegment(segmentSource, segmentTarget));
						segmentSource = segmentTarget;
					}

					break;
				}
				else if (link.Source == targetAncestors[i])
				{
					// Source is a direct ancestor of th target
					Node segmentSource = link.Source;

					// From source going down to target
					for (int j = i + 1; j < targetAncestors.Count; j++)
					{
						Node segmentTarget = targetAncestors[j];
						segments.Add(new LinkSegment(segmentSource, segmentTarget));
						segmentSource = segmentTarget;
					}

					break;
				}
				else if (link.Target == sourceAncestors[i])
				{
					// Target is a direct ancestor of the source
					Node segmentSource = link.Source;

					// From source going upp to target
					for (int j = sourceAncestors.Count - 2; j >= i ; j--)
					{
						Node segmentTarget = sourceAncestors[j];
						segments.Add(new LinkSegment(segmentSource, segmentTarget));
						segmentSource = segmentTarget;
					}

					break;
				}
			}

			return segments;
		}


		private static IEnumerable<Node> SourceAncestorsAndSel(Link link)
		{
			foreach (Node node in link.Source.AncestorsAndSelf())
			{
				yield return node;
			}

			yield return link.Source.Root;
		}


		private static IEnumerable<Node> TargetAncestorsAndSelf(Link link)
		{
			foreach (var node in link.Target.AncestorsAndSelf())
			{
				yield return node;
			}

			yield return link.Target.Root;
		}

	}
}