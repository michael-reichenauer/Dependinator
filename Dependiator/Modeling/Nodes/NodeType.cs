using Dependiator.Modeling.Serializing;
using Dependiator.Utils;


namespace Dependiator.Modeling.Nodes
{
	internal class NodeType : Equatable<NodeType>
	{
		public static readonly NodeType NameSpaceType = new NodeType(Data.NodeType.NameSpaceType);
		public static readonly NodeType TypeType = new NodeType(Data.NodeType.TypeType);
		public static readonly NodeType MemberType = new NodeType(Data.NodeType.MemberType);

		private readonly string type;


		public NodeType(string type)
		{
			this.type = type;
		}

		public static implicit operator NodeType(string text) => new NodeType(text);

		public static implicit operator string(NodeType nodeType) => nodeType?.type;

		protected override bool IsEqual(NodeType other) => type == other.type;
	}
}