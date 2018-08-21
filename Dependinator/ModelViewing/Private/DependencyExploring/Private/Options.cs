using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.DependencyExploring.Private
{
    internal class Options
    {
        public bool IsSource { get; }
        public Node SourceNode { get; }
        public Node TargetNode { get; }
        public Node SourceNode2 { get; }
        public Node TargetNode2 { get; }

        public IReadOnlyList<Node> HiddenSourceDependencies { get; }
        public IReadOnlyList<Node> HiddenTargetDependencies { get; }

        public Options(
            bool isSource,
            Node sourceNode,
            Node targetNode,
            IReadOnlyList<Node> hiddenSourceDependencies, 
            IReadOnlyList<Node> hiddenTargetDependencies)
        {
            IsSource = isSource;
            HiddenSourceDependencies = hiddenSourceDependencies;
            HiddenTargetDependencies = hiddenTargetDependencies;

            SourceNode = sourceNode == targetNode.Parent ? sourceNode.Root : sourceNode;
            SourceNode2 = sourceNode == targetNode.Parent
                ? targetNode.Parent
                : targetNode.Ancestors().Contains(sourceNode)
                    ? targetNode
                    : null;

            TargetNode = targetNode == sourceNode.Parent ? targetNode.Root : targetNode;
            TargetNode2 = targetNode == sourceNode.Parent
                ? sourceNode.Parent
                : targetNode.Ancestors().Contains(sourceNode)
                    ? null
                    : sourceNode;
        }
    }
}
