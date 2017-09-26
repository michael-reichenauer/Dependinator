using System.Collections.Generic;
using System.Windows;
using Dependinator.ModelParsing;
using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils;
using Point = System.Windows.Point;

namespace Dependinator.ModelViewing.Nodes
{
	internal class Node : Equatable<Node>
	{
		private readonly List<Node> children = new List<Node>();

		public Node(NodeName name)
		{
			Id = new NodeId(name);
			Name = name;
			if (Name.FullName == "AcmAcs")
			{
				
			}

			if (name == NodeName.Root)
			{
				Root = this;
				Parent = this;
			}

			IsEqualWhenSame(Id);
		}


		public bool CanShow => ViewModel?.CanShow ?? false;
		public bool IsShowing => ViewModel?.IsShowing ?? false;
		public int Stamp { get; set; }

		public NodeId Id { get; }
		public NodeName Name { get; }
		public string RootGroup { get; set; }

		public Node Parent { get; private set; }
		public Node Root { get; private set; }


		public IReadOnlyList<Node> Children => children;
		public ItemsCanvas ItemsCanvas { get; set; }

		public List<Link> SourceLinks { get; } = new List<Link>();

		public List<Line> SourceLines { get; } = new List<Line>();
		public List<Line> TargetLines { get; } = new List<Line>();

		public NodeType NodeType { get; set; }

		public NodeViewModel ViewModel { get; set; }

		public Rect Bounds { get; set; }
		public double Scale { get; set; }
		public Point Offset { get; set; }
		public string Color { get; set; }



		public void AddChild(Node child)
		{
			child.Parent = this;
			child.Root = Root;
			children.Add(child);
		}


		public void RemoveChild(Node child) => children.Remove(child);


		public override string ToString() => Name.ToString();
	}
}