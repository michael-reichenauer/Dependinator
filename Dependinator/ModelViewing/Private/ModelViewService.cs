using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Dependinator.Common;
using Dependinator.Common.ModelMetadataFolders.Private;
using Dependinator.Common.ProgressHandling;
using Dependinator.Common.SettingsHandling;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.ModelHandling;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.Nodes.Private;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private
{
	[SingleInstance]
	internal class ModelViewService : IModelViewService, ILoadModelService
	{
		private readonly ISettingsService settingsService;
		private readonly IModelService modelService;
		private readonly IItemSelectionService itemSelectionService;
		private readonly IProgressService progress;

		private ItemsCanvas rootNodeCanvas;

		public ModelViewService(
			ISettingsService settingsService,
			IModelService modelService,
			IItemSelectionService itemSelectionService,
			IProgressService progress)
		{
			this.settingsService = settingsService;
			this.modelService = modelService;
			this.itemSelectionService = itemSelectionService;
			this.progress = progress;
		}


		public void SetRootCanvas(ItemsCanvas rootCanvas)
		{
			this.rootNodeCanvas = rootCanvas;
			modelService.SetRootCanvas(rootCanvas);
		}


		public async Task LoadAsync()
		{
			Timing t = new Timing();

			Log.Debug("Loading repository ...");

			using (progress.ShowBusy())
			{
				RestoreViewSettings();

				await modelService.LoadAsync();
				t.Log("Updated view model after cached/fresh");
			}
		}


		public async Task RefreshAsync(bool refreshLayout) =>
			await modelService.RefreshAsync(refreshLayout);

		public IReadOnlyList<NodeName> GetHiddenNodeNames() => modelService.GetHiddenNodeNames();

		public void Clicked()
		{
			itemSelectionService.Clicked();
		}


		public void OnMouseWheel(UIElement uiElement, MouseWheelEventArgs e)
		{
			rootNodeCanvas?.OnMouseWheel(uiElement, e, false);
		}


		public void ShowHiddenNode(NodeName nodeName) => modelService.ShowHiddenNode(nodeName);


		public void Close()
		{
			StoreViewSettings();

			modelService.Save();
		}


		private void StoreViewSettings()
		{
			settingsService.Edit<WorkFolderSettings>(settings =>
			{
				settings.Scale = modelService.Root.View.ItemsCanvas.Scale;
				//settings.Offset = modelService.Root.ItemsCanvas.Offset;
			});
		}


		private void RestoreViewSettings()
		{
			WorkFolderSettings settings = settingsService.Get<WorkFolderSettings>();
			Node root = modelService.Root;

			root.View.ItemsCanvas.SetRootScale(settings.Scale);
			//root.ItemsCanvas.SetOffset(settings.Offset);

			//root.ItemsCanvas.SetRootScale(1);
			//root.ItemsCanvas.SetOffset(new Point(0, 0));
		}
	}
}