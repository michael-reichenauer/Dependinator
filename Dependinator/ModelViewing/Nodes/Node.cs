using System.Collections.Generic;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Nodes
{
	internal class Node : Equatable<Node>
	{
		private List<Node> children { get; set; } = new List<Node>();

		public Node(NodeName name, NodeType nodeType)
		{
			Id = new NodeId(name);
			Name = name;
			NodeType = nodeType;

			if (name == NodeName.Root)
			{
				Parent = this;
			}

			IsEqualWhen(Id);
		}


		public NodeId Id { get; }
		public Node Parent { get; private set; }
		public NodeName Name { get; }
		public NodeType NodeType { get; }

		public IReadOnlyList<Node> Children => children;

		public void AddChild(Node child)
		{
			child.Parent = this;
			children.Add(child);
		}

		public override string ToString() => Id.ToString();
	}
}