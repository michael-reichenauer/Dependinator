using System.Collections.Generic;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.ModelHandling.Private
{
    internal interface INodeService
    {
        Node Root { get; }

        IEnumerable<Node> AllNodes { get; }

        void AddNode(Node node, Node parentNode);

        bool TryGetNode(NodeName nodeName, out Node node);


        void UpdateNodeTypeIfNeeded(Node node, NodeType nodeType);

        Node GetParentNode(NodeName parentName, NodeType nodeType, bool isQueued);

        void RemoveNode(Node node);

        void QueueNode(DataNode dataNode);
        void RemoveAll();
        bool TryGetSavedNode(NodeName nodeName, out DataNode node);
        void SetIsChanged(Node node);
    }
}
