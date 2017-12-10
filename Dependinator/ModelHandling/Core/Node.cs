using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.Common;
using Dependinator.ModelHandling.Private.Items;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;
using Point = System.Windows.Point;

namespace Dependinator.ModelHandling.Core
{
	internal class Node : Equatable<Node>
	{
		public static readonly string Hidden = "hidden";

		private readonly List<Node> children = new List<Node>();

		public Node(NodeName name)
		{
			Name = name;

			if (name == NodeName.Root)
			{
				Root = this;
				Parent = this;
			}

			IsEqualWhenSame(Name);
		}


		public bool CanShow => ViewModel?.CanShow ?? false;
		public bool IsShowNode => ViewModel?.IsShowNode ?? false;
		public bool IsShowing => ViewModel?.IsShowing ?? false;
		public int Stamp { get; set; }

		public NodeName Name { get; }

		public Node Parent { get; private set; }
		public Node Root { get; private set; }
		public bool CanShowChildren => IsRoot || ViewModel.CanShowChildren;
		public bool IsLayoutCompleted { get; set; }

		public IReadOnlyList<Node> Children => children;
		public ItemsCanvas ItemsCanvas { get; set; }

		public List<Link> SourceLinks { get; } = new List<Link>();

		public List<Line> SourceLines { get; } = new List<Line>();
		public List<Line> TargetLines { get; } = new List<Line>();

		public NodeType NodeType { get; set; }

		public NodeViewModel ViewModel { get; set; }

		public Rect Bounds { get; set; }
		public double ScaleFactor { get; set; }
		public Point Offset { get; set; }
		public string Color { get; set; }
		public bool IsRoot => this == Root;
		public bool IsHidden { get; set; }
		public string Description { get; set; }


		public void AddChild(Node child)
		{
			child.Parent = this;
			child.Root = Root;
			children.Add(child);
		}


		public void RemoveChild(Node child) => children.Remove(child);


		public override string ToString() => Name.ToString();


		public void ShowHiddenNode()
		{
			IsHidden = false;
			Parent.ItemsCanvas?.UpdateAndNotifyAll();
		}

		public IEnumerable<Node> Ancestors()
		{
			Node current = this;

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


		public IEnumerable<Node> DescendentsBreadth()
		{
			Queue<Node> queue = new Queue<Node>();

			Children.ForEach(queue.Enqueue);

			while (queue.Any())
			{
				Node node = queue.Dequeue();
				yield return node;

				node.Children.ForEach(queue.Enqueue);
			}
		}
	}
}