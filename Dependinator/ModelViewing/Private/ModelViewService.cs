﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.Common.ThemeHandling;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Searching;


namespace Dependinator.ModelViewing.Private
{
	internal class ModelViewService : IModelViewService
	{
		private readonly ILocateService locateService;
		private readonly IModelViewModelService modelViewModelService;
		private readonly ISearchService searchService;
		private readonly IThemeService themeService;


		public ModelViewService(
			ILocateService locateService,
			IModelViewModelService modelViewModelService,
			ISearchService searchService,
			IThemeService themeService)
		{
			this.locateService = locateService;
			this.modelViewModelService = modelViewModelService;
			this.searchService = searchService;
			this.themeService = themeService;
		}


		public void StartMoveToNode(NodeName nodeName) => locateService.StartMoveToNode(nodeName);
		public IReadOnlyList<NodeName> GetHiddenNodeNames() => modelViewModelService.GetHiddenNodeNames();
		public void ShowHiddenNode(NodeName nodeName) => modelViewModelService.ShowHiddenNode(nodeName);
		public IEnumerable<NodeName> Search(string text) => searchService.Search(text);


		public async Task ActivateRefreshAsync()
		{
			themeService.SetThemeWpfColors();

			await Task.Yield();
		}


		public Task RefreshAsync(bool refreshLayout)=> modelViewModelService.RefreshAsync(refreshLayout);
		public void Close() => modelViewModelService.Close();
	}
}