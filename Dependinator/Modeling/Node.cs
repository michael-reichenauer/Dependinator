using Dependinator.Utils;

namespace Dependinator.Modeling
{
	internal abstract class Node : Equatable<Node>
	{
		protected Node(NodeId parentId, NodeName name)
		{
			Id = new NodeId(name);
			ParentId = parentId;
			Name = name;

			IsEqualWhen(Id);
		}


		public NodeId Id { get; }
		public NodeId ParentId { get; }
		public NodeName Name { get; }

		public override string ToString() => Id.ToString();
	}
}