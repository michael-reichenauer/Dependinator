using System.Collections.Generic;
using System.Threading.Tasks;


namespace Dependinator.ModelViewing
{
    internal interface IModelViewService
    {
        void StartMoveToNode(NodeName nodeName);

        void StartMoveToNode(string filePath);

        IReadOnlyList<NodeName> GetHiddenNodeNames();

        void ShowHiddenNode(NodeName nodeName);

        IEnumerable<NodeName> Search(string text);
        Task ActivateRefreshAsync();
        Task RefreshAsync(bool refreshLayout);
        Task CloseAsync();
    }
}
