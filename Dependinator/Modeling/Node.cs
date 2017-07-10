using Dependinator.Utils;

namespace Dependinator.Modeling
{
	internal abstract class Node : Equatable<Node>
	{
		protected Node(NodeId parentId, NodeName name)
		{
			Id = new NodeId(name);
			ParentId = parentId;

			IsEqualWhen(Id);
		}


		public NodeId Id { get; }
		public NodeId ParentId { get; }

		public override string ToString() => Id.ToString();
	}

	internal class RootNode : NamespaceNode
	{
		public RootNode()
			: base(NodeId.Root, NodeName.Root)
		{ }
	}

	internal class NamespaceNode : Node
	{
		public NamespaceNode(NodeId parentId, NodeName name) 
			: base(parentId, name)
		{
		}
	}

	internal class TypeNode : Node
	{
		public TypeNode(NodeId parentId, NodeName name)
			: base(parentId, name)
		{
		}
	}


	internal class MemberNode : Node
	{
		public MemberNode(NodeId parentId, NodeName name)
			: base(parentId, name)
		{
		}
	}
}