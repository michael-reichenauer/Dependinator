using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.Common;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;


namespace Dependinator.ModelViewing
{
    internal interface IModelViewService
    {
        void StartMoveToNode(NodeName nodeName);

        void StartMoveToNode(Source source);

        IReadOnlyList<NodeName> GetHiddenNodeNames();

        void ShowHiddenNode(NodeName nodeName);

        IEnumerable<NodeName> Search(string text);
        Task ActivateRefreshAsync();
        Task RefreshAsync(bool refreshLayout);
        Task CloseAsync();
        Task OpenModelAsync(ModelPaths modelPaths);
    }
}
