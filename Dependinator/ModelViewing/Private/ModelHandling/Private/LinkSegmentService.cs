using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.ModelHandling.Private
{
    internal class LinkSegmentService : ILinkSegmentService
    {
        public IEnumerable<LinkSegment> GetLinkSegments(Link link)
        {
            if (link.Source.Parent == link.Target.Parent ||
                link.Source == link.Target.Parent ||
                link.Source.Parent == link.Target)
            {
                // Sibling, parent or child link
                yield return new LinkSegment(link.Source, link.Target);
                yield break;
            }

            // Skipping root in both ancestors
            List<Node> sourceAncestors = link.Source.AncestorsAndSelf().Reverse().Skip(1).ToList();
            List<Node> targetAncestors = link.Target.AncestorsAndSelf().Reverse().Skip(1).ToList();

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
                        yield return new LinkSegment(segmentSource, segmentTarget);
                        segmentSource = segmentTarget;
                    }

                    // From common ancestor going down to target
                    for (int j = i; j < targetAncestors.Count; j++)
                    {
                        Node segmentTarget = targetAncestors[j];
                        yield return new LinkSegment(segmentSource, segmentTarget);
                        segmentSource = segmentTarget;
                    }

                    break;
                }

                if (link.Source == targetAncestors[i])
                {
                    // Source is a direct ancestor of th target
                    Node segmentSource = link.Source;

                    // From source going down to target
                    for (int j = i + 1; j < targetAncestors.Count; j++)
                    {
                        Node segmentTarget = targetAncestors[j];
                        yield return new LinkSegment(segmentSource, segmentTarget);
                        segmentSource = segmentTarget;
                    }

                    break;
                }

                if (link.Target == sourceAncestors[i])
                {
                    // Target is a direct ancestor of the source
                    Node segmentSource = link.Source;

                    // From source going upp to target
                    for (int j = sourceAncestors.Count - 2; j >= i; j--)
                    {
                        Node segmentTarget = sourceAncestors[j];
                        yield return new LinkSegment(segmentSource, segmentTarget);
                        segmentSource = segmentTarget;
                    }

                    break;
                }
            }
        }
    }
}
