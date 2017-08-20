using System.Collections.Generic;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Private
{

	[SingleInstance]
	internal class Model
	{
		private readonly Dictionary<NodeId, Node> nodesById = new Dictionary<NodeId, Node>();
		private readonly Dictionary<NodeName, Node> nodesByName = new Dictionary<NodeName, Node>();


		public Model()
		{
			Root = new Node(NodeName.Root);
			Root.NodeType = NodeType.NameSpace;

			Add(Root);
		}


		public Node Root { get; }

		public Node Node(NodeId nodeId) => nodesById[nodeId];
		public bool TryGetNode(NodeId nodeId, out Node node) => nodesById.TryGetValue(nodeId, out node);

		public Node Node(NodeName name) => nodesByName[name];
		public bool TryGetNode(NodeName name, out Node node) => nodesByName.TryGetValue(name, out node);

		public void Add(Node node)
		{
			nodesById[node.Id] = node;
			nodesByName[node.Name] = node;
		}

		public void Remove(Node node)
		{
			nodesById.Remove(node.Id);
			nodesByName.Remove(node.Name);
		}
	}
}