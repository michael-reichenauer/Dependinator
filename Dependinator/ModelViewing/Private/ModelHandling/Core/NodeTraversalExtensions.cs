using System.Collections.Generic;
using System.Linq;


namespace Dependinator.ModelViewing.Private.ModelHandling.Core
{
    internal static class NodeTraversalExtensions
    {
        public static IEnumerable<Node> Ancestors(this Node node)
        {
            Node current = node.Parent;

            while (current != node.Root)
            {
                yield return current;
                current = current.Parent;
            }

            yield return node.Root;
        }


        public static IEnumerable<Node> AncestorsAndSelf(this Node node)
        {
            yield return node;

            foreach (Node ancestor in node.Ancestors())
            {
                yield return ancestor;
            }
        }


        public static IEnumerable<Node> Descendents(this Node node)
        {
            Queue<Node> queue = new Queue<Node>();

            node.Children.ForEach(queue.Enqueue);

            while (queue.Any())
            {
                Node descendent = queue.Dequeue();
                yield return descendent;

                descendent.Children.ForEach(queue.Enqueue);
            }
        }


        public static IEnumerable<Node> DescendentsAndSelf(this Node node)
        {
            yield return node;

            foreach (Node descendent in node.Descendents())
            {
                yield return descendent;
            }
        }
    }
}
