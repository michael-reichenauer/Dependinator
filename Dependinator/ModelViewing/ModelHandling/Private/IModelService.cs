using System.Collections.Generic;
using Dependinator.ModelViewing.DataHandling;
using Dependinator.ModelViewing.DataHandling.Dtos;
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

		void QueueModelLink(NodeId targetId, DataLink dataLink);
		void QueueModelLine(NodeId targetId, DataLine dataLine);
		bool TryGetQueuedLinesAndLinks(NodeId nodeName, out IReadOnlyList<DataLine> readOnlyList, out IReadOnlyList<DataLink> modelLinks);
		void RemovedQueuedNode(NodeId nodeId);
		IReadOnlyList<DataNode> GetAllQueuedNodes();
		void QueueNode(DataNode dataNode);
	}
}


