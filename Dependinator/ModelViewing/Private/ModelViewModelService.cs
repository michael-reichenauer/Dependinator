using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.Common.ModelMetadataFolders.Private;
using Dependinator.Common.SettingsHandling;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.ModelHandling;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.Nodes;
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
		private readonly IItemSelectionService itemSelectionService;
		private readonly IOpenModelService openModelService;


		private ItemsCanvas rootNodeCanvas;

		public ModelViewModelService(
			ISettingsService settingsService,
			IModelHandlingService modelHandlingService,
			IItemSelectionService itemSelectionService,
			IOpenModelService openModelService)
		{
			this.settingsService = settingsService;
			this.modelHandlingService = modelHandlingService;
			this.itemSelectionService = itemSelectionService;
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

		public void Clicked() => itemSelectionService.Deselect();


		public void OnMouseWheel(UIElement uiElement, MouseWheelEventArgs e) =>
			rootNodeCanvas?.RootCanvas.ZoomNode(e);


		public void ShowHiddenNode(NodeName nodeName) => modelHandlingService.ShowHiddenNode(nodeName);


		public async Task OpenAsync() => await openModelService.OpenCurrentModelAsync();

		public Task OpenFilesAsync(IReadOnlyList<string> filePaths) =>
			openModelService.OpenModelAsync(filePaths);


		public void Close()
		{
			StoreViewSettings();

			modelHandlingService.Close();
		}


		private void StoreViewSettings()
		{
			settingsService.Edit<WorkFolderSettings>(settings =>
			{
				settings.Scale = modelHandlingService.Root.View.ItemsCanvas.Scale;
				settings.Offset = modelHandlingService.Root.View.ItemsCanvas.RootOffset;
			});
		}


		private void RestoreViewSettings()
		{
			WorkFolderSettings settings = settingsService.Get<WorkFolderSettings>();
			Node root = modelHandlingService.Root;

			root.View.ItemsCanvas.SetRootScale(settings.Scale);
			root.View.ItemsCanvas.SetRootOffset(settings.Offset);
		}
	}
}