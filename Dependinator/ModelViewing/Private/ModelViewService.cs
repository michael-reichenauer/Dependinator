using System.Threading.Tasks;
using Dependinator.Common.ProgressHandling;
using Dependinator.Common.SettingsHandling;
using Dependinator.ModelViewing.Nodes;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private
{
	[SingleInstance]
	internal class ModelViewService : IModelViewService
	{
		private readonly ISettings settings;
		private readonly IModelService modelService;
		private readonly IProgressService progress;


		public ModelViewService(
			ISettings settings,
			IModelService modelService,
			IProgressService progress)
		{
			this.settings = settings;
			this.modelService = modelService;
			this.progress = progress;
		}



		public void Init(ItemsCanvas rootCanvas)
		{
			modelService.Init(rootCanvas);
			Node modelServiceRoot = modelService.Root;
		}

		
		public async Task LoadAsync()
		{
			Timing t = new Timing();

			Log.Debug("Loading repository ...");

			using (progress.ShowBusy())
			{
				ItemsCanvas rootCanvas = modelService.Root.ItemsCanvas;

				modelService.ClearAll();

				modelService.Init(rootCanvas);

				RestoreViewSettings();

				await modelService.LoadAsync();
				t.Log("Updated view model after cached/fresh");
			}
		}



		public async Task Refresh(bool refreshLayout)
		{
			await modelService.RefreshAsync(refreshLayout);
		}



		public void Close()
		{
			StoreViewSettings();

			modelService.Save();
		}

		

		private void StoreViewSettings()
		{
			settings.Edit<WorkFolderSettings>(s =>
				{
					s.Scale = modelService.Root.ItemsCanvas.Scale;
					s.Offset = modelService.Root.ItemsCanvas.Offset;
				});
		}


		private void RestoreViewSettings()
		{
			WorkFolderSettings folderSettings = settings.Get<WorkFolderSettings>();
			Node modelServiceRoot = modelService.Root;
			modelServiceRoot.ItemsCanvas.Scale = folderSettings.Scale;
			modelServiceRoot.ItemsCanvas.Offset = folderSettings.Offset;
		}
	}
}