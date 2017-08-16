using System.Collections.Generic;
using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.ModelViewing.Private.Items.Private;
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

			IsEqualWhenSame(Id);
		}


		public NodeId Id { get; }
		public NodeName Name { get; }

		public Node Parent { get; private set; }
		public Node Root { get; private set; }

		public IReadOnlyList<Node> Children => children;
		public ItemsCanvas ItemsCanvas { get; set; }

		public List<Link> Links { get; } = new List<Link>();

		public List<Line> Lines { get; } = new List<Line>();

		public NodeType NodeType { get; }

		public NodeViewModel ViewModel { get; set; }


		public void AddChild(Node child)
		{
			child.Parent = this;
			child.Root = Root;
			children.Add(child);
		}


		public override string ToString() => Name.ToString();
	}
}