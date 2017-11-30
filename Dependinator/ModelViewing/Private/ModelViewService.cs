using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.Common;
using Dependinator.Common.ModelMetadataFolders.Private;
using Dependinator.Common.ProgressHandling;
using Dependinator.Common.SettingsHandling;
using Dependinator.ModelHandling;
using Dependinator.ModelHandling.Core;
using Dependinator.ModelHandling.Private.Items;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private
{
	[SingleInstance]
	internal class ModelViewService : IModelViewService, ILoadModelService
	{
		private readonly ISettingsService settingsService;
		private readonly IModelService modelService;
		private readonly IProgressService progress;


		public ModelViewService(
			ISettingsService settingsService,
			IModelService modelService,
			IProgressService progress)
		{
			this.settingsService = settingsService;
			this.modelService = modelService;
			this.progress = progress;
		}


		public void SetRootCanvas(ItemsCanvas rootCanvas) => modelService.SetRootCanvas(rootCanvas);


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
				settings.Scale = modelService.Root.ItemsCanvas.Scale;
				settings.Offset = modelService.Root.ItemsCanvas.Offset;
			});
		}


		private void RestoreViewSettings()
		{
			WorkFolderSettings settings = settingsService.Get<WorkFolderSettings>();
			Node root = modelService.Root;

			root.ItemsCanvas.SetRootScale(settings.Scale);
			root.ItemsCanvas.SetMoveOffset(settings.Offset);

			//root.ItemsCanvas.SetRootScale(1);
			//root.ItemsCanvas.SetOffset(new Point(0, 0));
		}
	}
}