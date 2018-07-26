using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.Common.ModelMetadataFolders.Private;
using Dependinator.Common.SettingsHandling;
using Dependinator.ModelViewing.Private.ItemsViewing;
using Dependinator.ModelViewing.Private.ModelHandling;
using Dependinator.ModelViewing.Private.ModelHandling.Core;
using Dependinator.Utils;
using Dependinator.Utils.Dependencies;
using Dependinator.Utils.Threading;


namespace Dependinator.ModelViewing.Private
{
	[SingleInstance]
	internal class ModelViewModelService : IModelViewModelService, ILoadModelService
	{
		private readonly ISettingsService settingsService;
		private readonly IModelHandlingService modelHandlingService;
		private readonly ISelectionService selectionService;
		private readonly IOpenModelService openModelService;


		private ItemsCanvas rootNodeCanvas;

		public ModelViewModelService(
			ISettingsService settingsService,
			IModelHandlingService modelHandlingService,
			ISelectionService selectionService,
			IOpenModelService openModelService)
		{
			this.settingsService = settingsService;
			this.modelHandlingService = modelHandlingService;
			this.selectionService = selectionService;
			this.openModelService = openModelService;
		}


		public void SetRootCanvas(ItemsCanvas rootCanvas)
		{
			this.rootNodeCanvas = rootCanvas;
			modelHandlingService.SetRootCanvas(rootCanvas);
		}


		public async Task LoadAsync()
		{
			Timing t = new Timing();

			Log.Debug("Loading repository ...");

			RestoreViewSettings();

			await modelHandlingService.LoadAsync();
			t.Log("Updated view model after cached/fresh");
		}


		public async Task RefreshAsync(bool refreshLayout)
		{
			await modelHandlingService.RefreshAsync(refreshLayout);
		}


		public IReadOnlyList<NodeName> GetHiddenNodeNames() => modelHandlingService.GetHiddenNodeNames();

		public void Clicked() => selectionService.Deselect();


		public void OnMouseWheel(UIElement uiElement, MouseWheelEventArgs e) =>
			rootNodeCanvas?.RootCanvas.Zoom(e);


		public void ShowHiddenNode(NodeName nodeName) => modelHandlingService.ShowHiddenNode(nodeName);


		public async Task OpenAsync() => await openModelService.OpenCurrentModelAsync();

		public Task OpenFilesAsync(IReadOnlyList<string> filePaths) =>
			openModelService.OpenModelAsync(filePaths);


		public void AddNewNode()
		{
			throw new System.NotImplementedException();
		}


		public void Close()
		{
			StoreViewSettings();

			modelHandlingService.Close();
		}


		private void StoreViewSettings()
		{
			settingsService.Edit<WorkFolderSettings>(settings =>
			{
				settings.Scale = modelHandlingService.Root.ItemsCanvas.Scale;
				settings.Offset = modelHandlingService.Root.ItemsCanvas.RootOffset;
			});
		}


		private void RestoreViewSettings()
		{
			WorkFolderSettings settings = settingsService.Get<WorkFolderSettings>();
			Node root = modelHandlingService.Root;

			root.ItemsCanvas.SetRootScale(settings.Scale);
			root.ItemsCanvas.SetRootOffset(settings.Offset);
		}
	}
}