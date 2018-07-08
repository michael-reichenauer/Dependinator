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

		bool TryGetNode(NodeName nodeName, out Node node);


		void SetLayoutDone();
		void RemoveAll();
		void RemoveObsoleteNodesAndLinks(int operationId);
		IReadOnlyList<NodeName> GetHiddenNodeNames();
		void ShowHiddenNode(NodeName nodeName);

		void AddOrUpdateItem(IDataItem item, int stamp);
		void HideNode(Node node);
		void AddLineViewModel(Line line);
	}
}


