using Dependinator.Utils;

namespace Dependinator.Modeling
{
	internal class Node : Equatable<Node>
	{
		public Node(NodeName name, string nodeType)
		{
			Id = new NodeId(name);
			ParentId = new NodeId(name.ParentName);
			Name = name;
			NodeType = nodeType;

			IsEqualWhen(Id);
		}


		public NodeId Id { get; }
		public NodeId ParentId { get; }
		public NodeName Name { get; }
		public string NodeType { get; }

		public override string ToString() => Id.ToString();
	}
}