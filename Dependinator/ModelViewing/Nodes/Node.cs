using Dependinator.Modeling;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Nodes
{
	internal class Node : Equatable<Node>
	{
		public Node(NodeName name, NodeType nodeType, NodeId parentId)
		{
			Id = new NodeId(name);
			ParentId = parentId;
			Name = name;
			NodeType = nodeType;

			IsEqualWhen(Id);
		}


		public NodeId Id { get; }
		public NodeId ParentId { get; }
		public NodeName Name { get; }
		public NodeType NodeType { get; }

		public override string ToString() => Id.ToString();
	}
}