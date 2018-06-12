using System.Collections.Generic;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal interface IModelService
	{
		Node Root { get; }

		IEnumerable<Node> AllNodes { get; }

		void Add(Node node);
		Node GetNode(NodeName name);
		bool TryGetNode(NodeName name, out Node node);

		void RemoveAll();
		void Remove(Node node);

		void QueueModelLink(NodeName targetName, ModelLink modelLink);
		void QueueModelLine(NodeName targetName, ModelLine modelLine);
		bool TryGetQueuedLinesAndLinks(NodeName nodeName, out IReadOnlyList<ModelLine> readOnlyList, out IReadOnlyList<ModelLink> modelLinks);
		void RemovedQueuedNode(NodeName nodeName);
		IReadOnlyList<ModelNode> GetAllQueuedNodes();
	}
}


