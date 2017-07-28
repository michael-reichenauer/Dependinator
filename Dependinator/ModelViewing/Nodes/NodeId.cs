using Dependinator.Modeling;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Nodes
{
	internal class NodeId : Equatable<NodeId>
	{
		private readonly string id;

		public static NodeId Root = new NodeId(NodeName.Root);

		public NodeId(string id)
		{
			this.id = id;
			IsEqualWhen(id);
		}

		public override string ToString() => this != Root ? id : "<root>";
	}
}