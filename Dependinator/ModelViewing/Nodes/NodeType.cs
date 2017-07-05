using Dependinator.Modeling.Private.Serializing;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Nodes
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
			IsEqualWhen(type);
		}

		public static implicit operator NodeType(string text) => new NodeType(text);

		public static implicit operator string(NodeType nodeType) => nodeType?.type;

		public override string ToString() => type;
	}
}