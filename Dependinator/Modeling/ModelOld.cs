using System.Collections.Generic;
using Dependinator.ModelViewing.Nodes;

namespace Dependinator.Modeling
{
	internal class ModelOld
	{
		private readonly Dictionary<NodeName, NodeOld> nodes = new Dictionary<NodeName, NodeOld>();


		public ModelOld(NodeOld root)
		{
			Root = root;
			nodes[root.NodeName] = root;
		}

		public NodeOld Root { get; }

		public IReadOnlyDictionary<NodeName, NodeOld> Nodes => nodes;


		public void AddNode(NodeOld node)
		{
			nodes[node.NodeName] = node;
		}
	}
}