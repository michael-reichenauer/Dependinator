using System.Collections.Generic;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.Nodes;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal interface IModelService
	{
		Node Root { get; }

		// ???
		IEnumerable<Node> AllNodes { get; }

		void SetIsChanged();



		// !! Ska nog bort
		IReadOnlyList<DataNode> GetAllQueuedNodes();



		//void Add(Node node);
		//Node GetNode(NodeName name);
		bool TryGetNode(NodeName nodeName, out Node node);

		//void RemoveAll();
		//void Remove(Node node);

		//void QueueModelLink(NodeName target, DataLink dataLink);
		//void QueueModelLine(NodeName target, DataLine dataLine);
		//bool TryGetQueuedLinesAndLinks(NodeName nodeName, out IReadOnlyList<DataLine> readOnlyList, out IReadOnlyList<DataLink> modelLinks);
		//void RemovedQueuedNode(NodeName nodeName);

		//void QueueNode(DataNode dataNode);
	}


	class ModelService : IModelService
	{
		private readonly IModelDatabase modelDatabase;


		public ModelService(IModelDatabase modelDatabase)
		{
			this.modelDatabase = modelDatabase;
		}


		public Node Root => modelDatabase.Root;
		public IEnumerable<Node> AllNodes => modelDatabase.AllNodes;
		public void SetIsChanged() => modelDatabase.SetIsChanged();


		// !! Ska nog bort
		public IReadOnlyList<DataNode> GetAllQueuedNodes() => modelDatabase.GetAllQueuedNodes();

		public bool TryGetNode(NodeName nodeName, out Node node) => modelDatabase.TryGetNode(nodeName, out node);

	}


	internal interface IModelDatabase
	{
		Node Root { get; }

		IEnumerable<Node> AllNodes { get; }

		void SetIsChanged();
		void Add(Node node);
		Node GetNode(NodeName name);
		bool TryGetNode(NodeName nodeName, out Node node);

		void RemoveAll();
		void Remove(Node node);

		void QueueModelLink(NodeName target, DataLink dataLink);
		void QueueModelLine(NodeName target, DataLine dataLine);
		bool TryGetQueuedLinesAndLinks(NodeName nodeName, out IReadOnlyList<DataLine> readOnlyList, out IReadOnlyList<DataLink> modelLinks);
		void RemovedQueuedNode(NodeName nodeName);
		IReadOnlyList<DataNode> GetAllQueuedNodes();
		void QueueNode(DataNode dataNode);
	}
}


