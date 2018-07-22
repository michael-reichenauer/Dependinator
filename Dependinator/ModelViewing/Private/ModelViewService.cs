using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.Common.ThemeHandling;
using Dependinator.ModelViewing.Private.DataHandling;
using Dependinator.ModelViewing.Private.Nodes;
using Dependinator.ModelViewing.Private.Searching;
using Dependinator.Utils;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private
{
	internal class ModelViewService : IModelViewService
	{
		private readonly ILocateNodeService locateNodeService;
		private readonly IModelViewModelService modelViewModelService;
		private readonly IDataDetailsService dataDetailsService;
		private readonly ISearchService searchService;
		private readonly IThemeService themeService;
		private readonly ModelMetadata modelMetadata;


		public ModelViewService(
			ILocateNodeService locateNodeService,
			IModelViewModelService modelViewModelService,
			IDataDetailsService dataDetailsService,
			ISearchService searchService,
			IThemeService themeService,
			ModelMetadata modelMetadata)
		{
			this.locateNodeService = locateNodeService;
			this.modelViewModelService = modelViewModelService;
			this.dataDetailsService = dataDetailsService;
			this.searchService = searchService;
			this.themeService = themeService;
			this.modelMetadata = modelMetadata;
		}


		public void StartMoveToNode(NodeName nodeName) => locateNodeService.TryStartMoveToNode(nodeName);

		public async void StartMoveToNode(string filePath)
		{
			R<NodeName> nodeName = await dataDetailsService.GetNodeForFilePathAsync(
				modelMetadata.ModelFilePath, filePath);

			if (nodeName.IsFaulted)
			{
				Log.Warn($"Failed to locate node for {filePath}");
				return;
			}

			Log.Debug($"Start to move to {nodeName.Value}");

			for (int i = 0; i < 10; i++)
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


		public Task RefreshAsync(bool refreshLayout)=> modelViewModelService.RefreshAsync(refreshLayout);
		public void Close() => modelViewModelService.Close();
	}
}