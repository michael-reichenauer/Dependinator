using System.Collections.Generic;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Searching;


namespace Dependinator.ModelViewing.Private
{
	internal class ModelViewService : IModelViewService
	{
		private readonly ILocateService locateService;
		private readonly IModelViewModelService modelViewModelService;
		private readonly ISearchService searchService;


		public ModelViewService(
			ILocateService locateService,
			IModelViewModelService modelViewModelService,
			ISearchService searchService)
		{
			this.locateService = locateService;
			this.modelViewModelService = modelViewModelService;
			this.searchService = searchService;
		}


		public void StartMoveToNode(NodeName nodeName) => locateService.StartMoveToNode(nodeName);
		public IReadOnlyList<NodeName> GetHiddenNodeNames() => modelViewModelService.GetHiddenNodeNames();
		public void ShowHiddenNode(NodeName nodeName) => modelViewModelService.ShowHiddenNode(nodeName);
		public IEnumerable<SearchEntry> Search(string text) => searchService.Search(text);

	}
}