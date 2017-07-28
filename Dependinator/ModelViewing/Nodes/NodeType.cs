using Dependinator.Modeling.Private.Serializing;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Nodes
{
	internal class NodeType : Equatable<NodeType>
	{
		public static readonly NodeType NameSpaceType = new NodeType(Data.NodeType.NameSpaceType);
		public static readonly NodeType TypeType = new NodeType(Data.NodeType.TypeType);
		public static readonly NodeType MemberType = new NodeType(Data.NodeType.MemberType);

		private readonly string typeName;


		public NodeType(string typeName)
		{
			this.typeName = typeName;
			IsEqualWhen(typeName);
		}

		public string AsString() => typeName;

		public override string ToString() => typeName;
	}
}