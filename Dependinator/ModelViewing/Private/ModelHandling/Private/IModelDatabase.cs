using System;
using System.Collections.Generic;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.ModelHandling.Private
{
    internal interface IModelDatabase
    {
        Node Root { get; }

        IEnumerable<Node> AllNodes { get; }
        bool IsChangeMonitored { get; set; }

        void SetIsChanged(Node node);
        void SetIsChanged(Line line);
        void Add(Node node);
        Node GetNode(NodeName name);
        bool TryGetNode(NodeName nodeName, out Node node);

        void RemoveAll();
        void Remove(Node node);

        void QueueModelLink(NodeName target, DataLink dataLink);
        void QueueModelLine(NodeName target, DataLine dataLine);


        bool TryGetQueuedLinesAndLinks(NodeName nodeName, out IReadOnlyList<DataLine> readOnlyList,
            out IReadOnlyList<DataLink> modelLinks);


        void RemovedQueuedNode(NodeName nodeName);
        IReadOnlyList<DataNode> GetAllQueuedNodes();
        void QueueNode(DataNode dataNode);
        event EventHandler DataModified;
        void SetSaveData(IReadOnlyList<IDataItem> savedItems);
        bool TryGetSavedNode(NodeName name, out DataNode node);
        bool TryGetSavedLine(LineId lineId, out DataLine line);
    }
}
