using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Dependinator.Common.ProgressHandling;
using Dependinator.Common.ThemeHandling;
using Dependinator.ModelViewing.Items;
using Dependinator.Utils;
using Dependinator.Utils.UI;

namespace Dependinator.ModelViewing
{
	[SingleInstance]
	internal class RootModelViewModel : ViewModel
	{
		private static readonly TimeSpan FilterDelay = TimeSpan.FromMilliseconds(300);

		private readonly IThemeService themeService;

		private readonly IModelViewService modelViewService;
		private readonly IProgressService progress;


		private readonly DispatcherTimer filterTriggerTimer = new DispatcherTimer();
		private readonly ItemsCanvas itemsCanvas = new ItemsCanvas(null, null);
		private string settingFilterText = "";

		private int width = 0;


		public RootModelViewModel(
			IModelViewService modelViewService,
			IThemeService themeService,
			IProgressService progressService)
		{
			this.modelViewService = modelViewService;
			this.themeService = themeService;
			this.progress = progressService;

			filterTriggerTimer.Tick += FilterTrigger;
			filterTriggerTimer.Interval = FilterDelay;

			ModelViewModel = new ModelViewModel(modelViewService, null, itemsCanvas);

		}


		public ModelViewModel ModelViewModel { get; }


		public async Task LoadAsync()
		{
			Timing t = new Timing();

			Log.Debug("Loading repository ...");

			using (progress.ShowDialog("Loading branch view ..."))
			{		
				modelViewService.InitModules(itemsCanvas);

				LoadViewModel();
				//Zoom(-120, new Point(1, 1), false);
				t.Log("Updated view model after cached/fresh");
			}

			await Task.Yield();
		}


		public void Zoom(double zoom, Point viewPosition)
		{
			modelViewService.Zoom(zoom, viewPosition);
		}


		public bool MoveCanvas(Vector viewOffset)
		{
			
			modelViewService.Move(viewOffset);
			return true;
		}




		public string FetchErrorText
		{
			get { return Get(); }
			set { Set(value); }
		}



		public string FilterText { get; private set; } = "";


	

		public int Width
		{
			get { return width; }
			set
			{
				if (width != value)
				{
					width = value;
					ModelViewModel.SizeChanged();
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
			using (progress.ShowDialog("Refreshing view ..."))
			{
				await modelViewService.Refresh(itemsCanvas, refreshLayout);
			}
		}



		private void UpdateViewModel()
		{
			Timing t = new Timing();

			if (!IsInFilterMode())
			{
				NotifyAll(); ;

				t.Log("Updated repository view model");
			}
		}


		private bool IsInFilterMode()
		{
			return !string.IsNullOrEmpty(FilterText) || !string.IsNullOrEmpty(settingFilterText);
		}


		private void LoadViewModel()
		{
			Timing t = new Timing();

			NotifyAll(); ;

			t.Log("Updated repository view model");
		}




		public int SelectedIndex
		{
			get { return Get(); }
			set { Set(value); }
		}


		public object SelectedItem
		{
			get { return Get().Value; }
			set { Set(value); }
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




		public void Clicked(Point viewPosition)
		{
			//Point canvasPosition = canvasService.GetCanvasPosition(viewPosition);

			//double clickX = position.X - 9;
			//double clickY = position.Y - 5;

			//int row = Converters.ToRow(clickY);

			//if (row < 0 || row >= Commits.Count - 1 || clickX < 0 || clickX >= graphWidth)
			//{
			//	// Click is not within supported area.
			//	return;
			//}

			//CommitViewModel commitViewModel = Commits[row];
			//int xDotCenter = commitViewModel.X;
			//int yDotCenter = commitViewModel.Y;

			//double absx = Math.Abs(xDotCenter - clickX);
			//double absy = Math.Abs(yDotCenter - clickY);

			//if ((absx < 10) && (absy < 10))
			//{
			//	Clicked(commitViewModel);
			//}
		}


		public void ClosingWindow()
		{
			modelViewService.Close();
		}
	}
}