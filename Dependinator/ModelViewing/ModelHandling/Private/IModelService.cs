using System.Collections.Generic;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal interface IModelService
	{
		Node Root { get; }

		IEnumerable<Node> AllNodes { get; }

		void Add(Node node);
		Node GetNode(NodeId id);
		bool TryGetNode(NodeId id, out Node node);

		void RemoveAll();
		void Remove(Node node);

		void QueueModelLink(NodeId targetId, ModelLink modelLink);
		void QueueModelLine(NodeId targetId, ModelLine modelLine);
		bool TryGetQueuedLinesAndLinks(NodeId nodeName, out IReadOnlyList<ModelLine> readOnlyList, out IReadOnlyList<ModelLink> modelLinks);
		void RemovedQueuedNode(NodeId nodeId);
		IReadOnlyList<ModelNode> GetAllQueuedNodes();
		void QueueNode(ModelNode modelNode);
	}
}


