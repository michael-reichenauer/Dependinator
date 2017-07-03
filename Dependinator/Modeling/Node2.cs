using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;

namespace Dependinator.Modeling
{
	internal abstract class Node2 : Equatable<Node2>
	{
		protected Node2(NodeId parentId, NodeName name)
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

	internal class NamespaceNode : Node2
	{
		public NamespaceNode(NodeId parentId, NodeName name) 
			: base(parentId, name)
		{
		}
	}

	internal class TypeNode : Node2
	{
		public TypeNode(NodeId parentId, NodeName name)
			: base(parentId, name)
		{
		}
	}


	internal class MemberNode : Node2
	{
		public MemberNode(NodeId parentId, NodeName name)
			: base(parentId, name)
		{
		}
	}
}