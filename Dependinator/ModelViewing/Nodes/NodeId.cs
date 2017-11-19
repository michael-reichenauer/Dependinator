using Dependinator.Common;
using Dependinator.ModelViewing.Private;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Nodes
{
	internal class NodeId : Equatable<NodeId>
	{
		private readonly NodeName nodeName;

		public static NodeId Root = new NodeId(NodeName.Root);

		public NodeId(NodeName nodeName)
		{
			this.nodeName = nodeName;
			IsEqualWhenSame(nodeName);
		}

		public override string ToString() => this != Root ? nodeName.ToString() : "<root>";
	}
}