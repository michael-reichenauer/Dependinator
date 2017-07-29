using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using Dependinator.Common.ProgressHandling;
using Dependinator.Common.ThemeHandling;
using Dependinator.ModelViewing.Private;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.ModelViewing.Private.Items.Private;
using Dependinator.Utils;
using Dependinator.Utils.UI;

namespace Dependinator.ModelViewing
{
	[SingleInstance]
	internal class ModelViewModel : ViewModel
	{
		private static readonly TimeSpan FilterDelay = TimeSpan.FromMilliseconds(300);

		private readonly IThemeService themeService;

		private readonly IModelViewService modelViewService;
		private readonly IProgressService progress;


		private readonly DispatcherTimer filterTriggerTimer = new DispatcherTimer();
		private string settingFilterText = "";

		private int width = 0;


		public ModelViewModel(
			IModelViewService modelViewService,
			//IItemsService itemsService,
			IThemeService themeService,
			IProgressService progressService)
		{
			this.modelViewService = modelViewService;
			this.themeService = themeService;
			this.progress = progressService;

			filterTriggerTimer.Tick += FilterTrigger;
			filterTriggerTimer.Interval = FilterDelay;

			ItemsViewModel = new ItemsViewModel(new ItemsCanvas());
		}


		public ItemsViewModel ItemsViewModel { get; }


		public string FetchErrorText { get => Get(); set => Set(value); }

		public string FilterText { get; private set; } = "";

		public int SelectedIndex { get => Get(); set => Set(value); }

		public object SelectedItem { get => Get().Value; set => Set(value); }


		public async Task LoadAsync()
		{
			Timing t = new Timing();

			Log.Debug("Loading repository ...");

			using (progress.ShowBusy())
			{
				await modelViewService.LoadAsync(ItemsViewModel.ItemsCanvas);

				t.Log("Updated view model after cached/fresh");
			}
		}


		public int Width
		{
			get => width;
			set
			{
				if (width != value)
				{
					width = value;
					ItemsViewModel.SizeChanged();
				}
			}
		}


		public void RefreshView()
		{
			UpdateViewModel();
		}


		public async Task ActivateRefreshAsync()
		{
			Log.Usage("Activate window");

			Timing t = new Timing();
			themeService.SetThemeWpfColors();
			t.Log("SetThemeWpfColors");

			using (progress.ShowBusy())
			{
				await Task.Yield();
			}

			t.Log("Activate refresh done");
		}


		public async Task AutoRemoteCheckAsync()
		{
			Timing t = new Timing();
			Log.Usage("Automatic remote check");
			await Task.Yield();
			t.Log("Auto refresh done");
		}


		public async Task ManualRefreshAsync(bool refreshLayout = false)
		{
			using (progress.ShowBusy())
			{
				await modelViewService.Refresh(ItemsViewModel.ItemsCanvas, refreshLayout);
			}
		}


		private void UpdateViewModel()
		{
			Timing t = new Timing();

			if (!IsInFilterMode())
			{
				NotifyAll();
				;

				t.Log("Updated repository view model");
			}
		}


		private bool IsInFilterMode()
		{
			return !string.IsNullOrEmpty(FilterText) || !string.IsNullOrEmpty(settingFilterText);
		}


		public void SetFilter(string text)
		{
			filterTriggerTimer.Stop();
			Log.Debug($"Filter: {text}");
			settingFilterText = (text ?? "").Trim();
			filterTriggerTimer.Start();
		}


		private void FilterTrigger(object sender, EventArgs e)
		{
			//VirtualItemsSource.DataChanged();
		}


		public void ClosingWindow()
		{
			modelViewService.Close();
		}
	}
}