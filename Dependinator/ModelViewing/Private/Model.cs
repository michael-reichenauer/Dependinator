using System.Collections.Generic;
using Dependinator.Common;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Private
{

	[SingleInstance]
	internal class Model
	{
		private readonly Dictionary<NodeName, Node> nodes = new Dictionary<NodeName, Node>();


		public Model()
		{
			AddRoot();
		}


		public Node Root { get; private set; }

		public Node Node(NodeName name) => nodes[name];

		public bool TryGetNode(NodeName name, out Node node) => nodes.TryGetValue(name, out node);

		public void Add(Node node) => nodes[node.Name] = node;

		public void Remove(Node node) => nodes.Remove(node.Name);


		public void RemoveAll()
		{
			ItemsCanvas rootCanvas = Root.ItemsCanvas;
			nodes.Clear();

			AddRoot();
			Root.ItemsCanvas = rootCanvas;
		}


		private void AddRoot()
		{
			Root = new Node(NodeName.Root);
			Root.NodeType = NodeType.NameSpace;

			Add(Root);
		}
	}
}