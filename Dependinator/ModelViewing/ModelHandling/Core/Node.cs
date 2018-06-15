using System;
using System.Collections.Generic;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.ModelHandling.Core
{
	internal class Node : Equatable<Node>
	{
		public static readonly string Hidden = "hidden";

		private readonly List<Node> children = new List<Node>();

		public Node(NodeName name)
		{
			Name = name;
			Id = new NodeId(name);

			Init(name);
			IsEqualWhenSame(Name);
		}
		

		public int Stamp { get; set; }

		public NodeName Name { get; }
		public NodeId Id { get; }
		public NodeType NodeType { get; set; }
		public string Description { get; set; }
		public Lazy<string> CodeText { get; set; }

		public Node Root { get; private set; }
		public Node Parent { get; private set; }
		public IReadOnlyList<Node> Children => children;


		public List<Link> SourceLinks { get; } = new List<Link>();
		public List<Link> TargetLinks { get; } = new List<Link>();

		public List<Line> SourceLines { get; } = new List<Line>();
		public List<Line> TargetLines { get; } = new List<Line>();

		public bool IsRoot => this == Root;

		public NodeViewData View { get; private set; }

		public void AddChild(Node child)
		{
			child.Parent = this;
			child.Root = Root;
			children.Add(child);
		}
	

		public void RemoveChild(Node child) => children.Remove(child);


		public override string ToString() => Name.ToString();


		private void Init(NodeName name)
		{
			if (name == NodeName.Root)
			{
				Root = this;
				Parent = this;
			}

			View = new NodeViewData(this);
		}
	}
}