using Dependinator.ModelParsing;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Nodes
{
	internal class NodeType : Equatable<NodeType>
	{
		public static readonly NodeType NameSpace = new NodeType(JsonTypes.NodeType.NameSpace);
		public static readonly NodeType Type = new NodeType(JsonTypes.NodeType.Type);
		public static readonly NodeType Member = new NodeType(JsonTypes.NodeType.Member);
		public static readonly NodeType Referenced = new NodeType("Referenced");

		private readonly string typeName;


		public NodeType(string typeName)
		{
			this.typeName = typeName;
			IsEqualWhenSame(typeName);
		}

		public string AsString() => typeName;

		public override string ToString() => typeName;
	}
}