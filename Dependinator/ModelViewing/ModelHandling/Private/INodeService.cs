using System.Collections.Generic;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.Nodes;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal interface INodeService
	{
		Node Root { get; }

		IEnumerable<Node> AllNodes { get; }

		void AddNode(Node node, Node parentNode);

		bool TryGetNode(NodeName nodeName, out Node node);


		void UpdateNodeTypeIfNeeded(Node node, NodeType nodeType);

		Node GetParentNode(NodeName parentName, NodeType nodeType);

		void RemoveNode(Node node);

		void QueueNode(DataNode dataNode);
		void RemoveAll();
	}
}