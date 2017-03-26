using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Dependiator.Common.ProgressHandling;
using Dependiator.Common.ThemeHandling;
using Dependiator.Modeling;
using Dependiator.Utils;
using Dependiator.Utils.UI;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.MainViews
{
	[SingleInstance]
	internal class MainViewModel : ViewModel
	{
		private static readonly TimeSpan FilterDelay = TimeSpan.FromMilliseconds(300);

		private readonly IThemeService themeService;

		private readonly IModelService modelService;
		private readonly IProgressService progress;


		private readonly DispatcherTimer filterTriggerTimer = new DispatcherTimer();
		private string settingFilterText = "";

		private int width = 0;



		public MainViewModel(
			IModelService modelService,
			IThemeService themeService,
			IProgressService progressService)
		{
			this.modelService = modelService;
			this.themeService = themeService;
			this.progress = progressService;

			filterTriggerTimer.Tick += FilterTrigger;
			filterTriggerTimer.Interval = FilterDelay;
		}


		public NodesViewModel NodesViewModel { get; } = new NodesViewModel(null, null);


		public async Task LoadAsync()
		{
			Timing t = new Timing();

			Log.Debug("Loading repository ...");

			using (progress.ShowDialog("Loading branch view ..."))
			{		
				modelService.InitModules(NodesViewModel.ItemsCanvas);

				LoadViewModel();
				//Zoom(-120, new Point(1, 1), false);
				t.Log("Updated view model after cached/fresh");
			}

			await Task.Yield();
		}


		public bool Zoom(int zoomDelta, Point viewPosition, bool isNodeZoom)
		{
			if (!isNodeZoom)
			{				
				modelService.Zoom(zoomDelta, viewPosition);
				return true;
			}

			return false;
			//else
			//{
			//	return modelService.ZoomNode(zoomDelta, viewPosition);
			//}
		}


		public bool MoveCanvas(Vector viewOffset)
		{
			
			modelService.Move(viewOffset);
			return true;
		}


		public object MoveNode(Point viewPosition, Vector viewOffset, object movingObject)
		{
			return modelService.MoveNode(viewPosition, viewOffset, movingObject);
		}




		public void ShowCommitDetails()
		{
			IsShowCommitDetails = true;
		}


		public void ToggleCommitDetails()
		{
			IsShowCommitDetails = !IsShowCommitDetails;
		}


		public string FetchErrorText
		{
			get { return Get(); }
			set { Set(value); }
		}


		public Command ToggleDetailsCommand => Command(ToggleCommitDetails);


		public string FilterText { get; private set; } = "";


		public bool IsShowCommitDetails
		{
			get { return Get(); }
			set { Set(value); }
		}


		public int Width
		{
			get { return width; }
			set
			{
				if (width != value)
				{
					width = value;
					NodesViewModel.SizeChanged();
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


		private void OnRepositoryUpdated()
		{
			Log.Debug("Update repository view model after updated Repository");
			Timing t = new Timing();
			using (progress.ShowBusy())
			{
			}

			t.Log("Updated view model after updated repository");
		}


		public async Task ManualRefreshAsync()
		{
			using (progress.ShowDialog("Refreshing view ..."))
			{
				await modelService.Refresh(NodesViewModel.ItemsCanvas);
			}
		}


		//public void MouseEnterBranch(BranchViewModel branch)
		//{

		//	//}
		//}


		//public void MouseLeaveBranch(BranchViewModel branch)
		//{

		//}


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


		public void ScrollRows(int rows)
		{
			//int offsetY = Converters.ToY(rows);
			//Canvas.Offset = new Point(Canvas.Offset.X, Math.Max(Canvas.Offset.Y - offsetY, 0));
		}


		private void ScrollTo(int rows)
		{
			//int offsetY = Converters.ToY(rows);
			//Canvas.Offset = new Point(Canvas.Offset.X, Math.Max(offsetY, 0));
		}


		public void ShowUncommittedDetails()
		{
			SelectedIndex = 0;
			ScrollTo(0);
			IsShowCommitDetails = true;
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
			modelService.Close();
		}
	}
}