using Dependinator.ModelParsing.Private.Serializing;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Nodes
{
	internal class NodeType : Equatable<NodeType>
	{
		public static readonly NodeType NameSpace = new NodeType(Dtos.NodeType.NameSpace);
		public static readonly NodeType Type = new NodeType(Dtos.NodeType.Type);
		public static readonly NodeType Member = new NodeType(Dtos.NodeType.Member);

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