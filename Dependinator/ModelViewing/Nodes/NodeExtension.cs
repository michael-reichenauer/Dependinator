using System.Collections.Generic;

namespace Dependinator.ModelViewing.Nodes
{
	internal static class NodeExtension
	{
		public static IEnumerable<Node> Ancestors(this Node node)
		{
			Node current = node;

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
			yield return node;

			var last = node;
			foreach (var descendent in DescendentsBreadth(node))
			{
				foreach (var child in descendent.Children)
				{
					yield return child;
					last = child;
				}

				if (last.Equals(descendent))
				{
					yield break;
				}
			}
		}
	}
}