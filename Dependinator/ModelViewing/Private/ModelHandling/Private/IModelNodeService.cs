using System.Collections.Generic;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.ModelHandling.Private
{
    internal interface IModelNodeService
    {
        void AddOrUpdateNode(DataNode dataNode, int id);
        void RemoveObsoleteNodesAndLinks(int stamp);
        void SetLayoutDone();
        void ShowHiddenNode(NodeName nodeName);
        IReadOnlyList<NodeName> GetHiddenNodeNames();
        void HideNode(Node node);
        void RemoveAll();
    }
}
