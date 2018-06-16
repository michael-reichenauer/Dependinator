using System.Collections.Generic;
using Dependinator.ModelViewing.ModelDataHandling;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal interface INodeService
	{
		Node Root { get; }

		IEnumerable<Node> AllNodes { get; }

		void AddNode(Node node, Node parentNode);

		bool TryGetNode(NodeId nodeId, out Node node);


		void UpdateNodeTypeIfNeeded(Node node, NodeType nodeType);

		Node GetParentNode(NodeName parentName, NodeType nodeType);

		void RemoveNode(Node node);

		void RemoveAll();
		void QueueNode(ModelNode modelNode);
	}
}