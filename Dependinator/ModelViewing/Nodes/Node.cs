using System.Collections.Generic;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Nodes
{
	internal class Node : Equatable<Node>
	{
		private readonly List<Node> children = new List<Node>();

		public Node(NodeName name, NodeType nodeType)
		{
			Id = new NodeId(name);
			Name = name;
			NodeType = nodeType;

			if (name == NodeName.Root)
			{
				Root = this;
				Parent = this;
			}

			IsEqualWhen(Id);
		}


		public NodeId Id { get; }
		public Node Parent { get; private set; }
		public Node Root { get; private set; }
		public NodeName Name { get; }
		public NodeType NodeType { get; }

		public IReadOnlyList<Node> Children => children;

		public IItemsCanvas ChildrenCanvas { get; set; }

		public NodeViewModel ViewModel { get; set; }


		public void AddChild(Node child)
		{
			child.Parent = this;
			child.Root = Root;
			children.Add(child);
		}


		public IEnumerable<Node> Ancestors()
		{
			Node current = Parent;

			while (current != Root)
			{
				yield return current;
				current = current.Parent;
			}
		}


		public IEnumerable<Node> AncestorsAndSelf()
		{
			yield return this;

			foreach (Node ancestor in Ancestors())
			{
				yield return ancestor;
			}
		}

		public IEnumerable<Node> Descendents()
		{
			foreach (Node child in Children)
			{
				yield return child;

				foreach (Node descendent in child.Descendents())
				{
					yield return descendent;
				}
			}
		}

		public IEnumerable<Node> DescendentsAndSelf()
		{
			yield return this;

			foreach (Node descendent in Descendents())
			{
				yield return descendent;
			}
		}

		public override string ToString() => Id.ToString();
	}
}