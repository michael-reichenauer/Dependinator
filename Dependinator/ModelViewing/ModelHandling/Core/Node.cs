using System.Collections.Generic;
using System.Windows;
using Dependinator.Common;
using Dependinator.ModelViewing.ModelHandling.Private.Items;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;
using Point = System.Windows.Point;

namespace Dependinator.ModelViewing.ModelHandling.Core
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

	}
}