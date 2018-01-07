using System.Collections.Generic;
using Dependinator.Common;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal interface INodeService
	{
		Node Root { get; }

		IEnumerable<Node> AllNodes { get; }

		void AddNode(Node node, Node parentNode);

		bool TryGetNode(NodeName name, out Node node);


		void UpdateNodeTypeIfNeeded(Node node, NodeType nodeType);

		Node GetParentNode(NodeName parentName, NodeType nodeType);

		void RemoveNode(Node node);

		void RemoveAll();
	}
}