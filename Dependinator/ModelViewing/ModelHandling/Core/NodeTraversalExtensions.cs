using System.Collections.Generic;
using System.Linq;


namespace Dependinator.ModelViewing.ModelHandling.Core
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
			foreach (Node child in node.Children)
			{
				yield return child;

				foreach (Node descendent in child.Descendents())
				{
					yield return descendent;
				}
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


		public static IEnumerable<Node> DescendentsBreadth(this Node node)
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
	}
}