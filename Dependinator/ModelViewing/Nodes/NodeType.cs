using Dependinator.Modeling.Private.Serializing;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Nodes
{
	internal class NodeType : Equatable<NodeType>
	{
		public static readonly NodeType NameSpace = new NodeType(Data.NodeType.NameSpace);
		public static readonly NodeType Type = new NodeType(Data.NodeType.Type);
		public static readonly NodeType Member = new NodeType(Data.NodeType.Member);

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