using Dependinator.Modeling.Private.Serializing;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Nodes
{
	internal class NodeTypeOld : Equatable<NodeTypeOld>
	{
		public static readonly NodeTypeOld NameSpaceType = new NodeTypeOld(Data.NodeType.NameSpace);
		public static readonly NodeTypeOld TypeType = new NodeTypeOld(Data.NodeType.Type);
		public static readonly NodeTypeOld MemberType = new NodeTypeOld(Data.NodeType.Member);

		private readonly string type;


		public NodeTypeOld(string type)
		{
			this.type = type;
			IsEqualWhenSame(type);
		}

		public static implicit operator NodeTypeOld(string text) => new NodeTypeOld(text);

		public static implicit operator string(NodeTypeOld nodeType) => nodeType?.type;

		public override string ToString() => type;
	}
}