﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.Common;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.Common.ThemeHandling;
using Dependinator.ModelViewing.Private.CodeViewing;
using Dependinator.ModelViewing.Private.DataHandling;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.Nodes;
using Dependinator.ModelViewing.Private.Searching;
using Dependinator.Utils;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private
{
    internal class ModelViewService : IModelViewService
    {
        private readonly IDataService dataService;
        private readonly ILocateNodeService locateNodeService;
        private readonly ModelMetadata modelMetadata;
        private readonly IModelViewModelService modelViewModelService;
        private readonly ISearchService searchService;
        private readonly IThemeService themeService;
        private readonly ISolutionService solutionService;


        public ModelViewService(
            ILocateNodeService locateNodeService,
            IModelViewModelService modelViewModelService,
            IDataService dataService,
            ISearchService searchService,
            IThemeService themeService,
            ISolutionService solutionService,
            ModelMetadata modelMetadata)
        {
            this.locateNodeService = locateNodeService;
            this.modelViewModelService = modelViewModelService;
            this.dataService = dataService;
            this.searchService = searchService;
            this.themeService = themeService;
            this.solutionService = solutionService;
            this.modelMetadata = modelMetadata;
        }


        public void StartMoveToNode(NodeName nodeName) => locateNodeService.TryStartMoveToNode(nodeName);


        public async void StartMoveToNode(Source source)
        {
    
            M<DataNodeName> nodeName = await dataService.TryGetNodeAsync(
                modelMetadata.ModelPaths, source);

            if (nodeName.IsFaulted)
            {
                Log.Warn($"Failed to locate node for {source}");
                return;
            }

            Log.Debug($"Start to move to {nodeName.Value}");

            for (int i = 0; i < 25; i++)
            {
                if (locateNodeService.TryStartMoveToNode(nodeName.Value))
                {
                    return;
                }

                await Task.Delay(1000);
            }
        }


        public IReadOnlyList<NodeName> GetHiddenNodeNames() => modelViewModelService.GetHiddenNodeNames();
        public void ShowHiddenNode(NodeName nodeName) => modelViewModelService.ShowHiddenNode(nodeName);
        public IEnumerable<NodeName> Search(string text) => searchService.Search(text);


        public async Task ActivateRefreshAsync()
        {
            themeService.SetThemeWpfColors();

            await Task.Yield();
        }


        public Task RefreshAsync(bool refreshLayout) => modelViewModelService.RefreshAsync(refreshLayout);
        public Task CloseAsync() => modelViewModelService.CloseAsync();
        public Task OpenModelAsync(ModelPaths modelPaths) => solutionService.OpenModelAsync(modelPaths);
    }
}
