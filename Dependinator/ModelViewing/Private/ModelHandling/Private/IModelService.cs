using System.Collections.Generic;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.ModelHandling.Private
{
    internal interface IModelService
    {
        Node Root { get; }

        // ???
        IEnumerable<Node> AllNodes { get; }

        void SetIsChanged(Node node);


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
