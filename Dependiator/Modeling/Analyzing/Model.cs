using System.Collections.Generic;


namespace Dependiator.Modeling.Analyzing
{
	internal class Model
	{
		private readonly Dictionary<string, Node> nodes  = new Dictionary<string, Node>();


		public Model(Node root)
		{
			Root = root;
			nodes[root.NodeName.FullName] = root;
		}

		public Node Root { get; }

		public IReadOnlyDictionary<string, Node> Nodes => nodes;


		public void AddNode(Node node)
		{
			nodes[node.NodeName.FullName] = node;
		}
	}
}