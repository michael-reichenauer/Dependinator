using System.Collections.Generic;


namespace Dependiator.Modeling
{
	internal class Model
	{
		private readonly Dictionary<NodeName, Node> nodes  = new Dictionary<NodeName, Node>();


		public Model(Node root)
		{
			Root = root;
			nodes[root.NodeName] = root;
		}

		public Node Root { get; }

		public IReadOnlyDictionary<NodeName, Node> Nodes => nodes;


		public void AddNode(Node node)
		{
			nodes[node.NodeName] = node;
		}
	}
}