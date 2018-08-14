using System.Collections.Generic;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.ModelHandling.Core;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private.ModelHandling.Private
{
    internal class ModelService : IModelService
    {
        private readonly IModelDatabase modelDatabase;
        private readonly IModelLineService modelLineService;
        private readonly IModelNotificationService modelNotificationService;
        private readonly IModelLinkService modelLinkService;
        private readonly IModelNodeService modelNodeService;


        public ModelService(
            IModelDatabase modelDatabase,
            IModelNodeService modelNodeService,
            IModelLinkService modelLinkService,
            IModelLineService modelLineService,
            IModelNotificationService modelNotificationService)
        {
            this.modelDatabase = modelDatabase;
            this.modelNodeService = modelNodeService;
            this.modelLinkService = modelLinkService;
            this.modelLineService = modelLineService;
            this.modelNotificationService = modelNotificationService;
        }


        public Node Root => modelDatabase.Root;
        public IEnumerable<Node> AllNodes => modelDatabase.AllNodes;
        public void SetIsChanged(Node node) => modelDatabase.SetIsChanged(node);


        // !! Ska nog bort
        public IReadOnlyList<DataNode> GetAllQueuedNodes() => modelDatabase.GetAllQueuedNodes();

        public bool TryGetNode(NodeName nodeName, out Node node) => modelDatabase.TryGetNode(nodeName, out node);
        public void SetLayoutDone() => modelNodeService.SetLayoutDone();
        public void RemoveAll() => modelNodeService.RemoveAll();


        public void RemoveObsoleteNodesAndLinks(int operationId) =>
            modelNodeService.RemoveObsoleteNodesAndLinks(operationId);


        public IReadOnlyList<NodeName> GetHiddenNodeNames() => modelNodeService.GetHiddenNodeNames();
        public void ShowHiddenNode(NodeName nodeName)
        {
            modelNodeService.ShowHiddenNode(nodeName);
            modelNotificationService.TriggerNotification();
        }


        public void AddOrUpdateItem(IDataItem item, int stamp)
        {
            switch (item)
            {
                case DataLine line:
                    modelLineService.AddOrUpdateLine(line, stamp);
                    break;
                case DataLink link:
                    modelLinkService.AddOrUpdateLink(link, stamp);
                    break;
                case DataNode node:
                    modelNodeService.AddOrUpdateNode(node, stamp);
                    break;
                default:
                    throw Asserter.FailFast($"Unknown item type {item}");
            }
        }


        public void HideNode(Node node)
        {
            modelNodeService.HideNode(node);
            modelNotificationService.TriggerNotification();
        }


        public void AddLineViewModel(Line line) => modelLineService.AddLineViewModel(line);
        public void SetSaveData(IReadOnlyList<IDataItem> savedItems)
        {

            modelDatabase.SetSaveData(savedItems);
        }
    }
}
