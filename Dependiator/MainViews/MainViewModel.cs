using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Dependiator.ApplicationHandling;
using Dependiator.Common.ProgressHandling;
using Dependiator.Common.ThemeHandling;
using Dependiator.MainViews.Private;
using Dependiator.Utils;
using Dependiator.Utils.UI;
using Dependiator.Utils.UI.VirtualCanvas;



namespace Dependiator.MainViews
{
	/// <summary>
	/// View model
	/// </summary>
	[SingleInstance]
	internal class MainViewModel : ViewModel
	{
		private static readonly TimeSpan FilterDelay = TimeSpan.FromMilliseconds(300);

		private readonly IThemeService themeService;
		private readonly WorkingFolder workingFolder;
		private readonly IProgressService progress;


		private readonly DispatcherTimer filterTriggerTimer = new DispatcherTimer();
		private string settingFilterText = "";

		private int width = 0;
	
		public List<ModuleViewModel> Modules { get; } = new List<ModuleViewModel>();

		private readonly AsyncLock refreshLock = new AsyncLock();


		public ZoomableCanvas Canvas { get; set; }
		public System.Windows.Controls.ListBox ListBox { get; set; }

		public MainViewModel(
			WorkingFolder workingFolder,
			IThemeService themeService,
			IProgressService progressService)
		{
			this.workingFolder = workingFolder;
			this.themeService = themeService;
			this.progress = progressService;

			VirtualItemsSource = new MainViewVirtualItemsSource(Modules);

			filterTriggerTimer.Tick += FilterTrigger;
			filterTriggerTimer.Interval = FilterDelay;

			Modules.Add(new ModuleViewModel
			{
				Brush = Brushes.Aqua,
				Rect = new Rect(100, 100, 100, 100),
				Rectangle = new Rect(2, 2, 70, 70),
			});

			Modules.Add(new ModuleViewModel
			{
				Brush = Brushes.CornflowerBlue,
				Rect = new Rect(400, 200, 100, 100),
				Rectangle = new Rect(2, 2, 20, 70),
			});
		}

		public MainViewVirtualItemsSource VirtualItemsSource { get; }

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
					VirtualItemsSource.DataChanged();
				}
			}
		}



		public void RefreshView()
		{
			UpdateViewModel();
		}


		public async Task LoadAsync()
		{
			Timing t = new Timing();

			using (await refreshLock.LockAsync())
			{
				Log.Debug("Loading repository ...");

				using (progress.ShowDialog("Loading branch view ..."))
				{
					t.Log("Read cached/fresh repository");
					LoadViewModel();
					t.Log("Updated view model after cached/fresh");
				}
			}
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
				await Task.Yield();
			}
		}


		//public void MouseEnterBranch(BranchViewModel branch)
		//{
		//	branch.SetHighlighted();

		//	//if (branch.Branch.IsLocalPart)
		//	//{
		//	//	// Local part branch, then do not dim common commits in main branch part
		//	//	foreach (CommitViewModel commit in Commits)
		//	//	{
		//	//		if (commit.Commit.Branch.Id != branch.Branch.Id
		//	//			&& !(commit.Commit.IsCommon
		//	//				&& commit.Commit.Branch.IsMainPart
		//	//				&& commit.Commit.Branch.LocalSubBranch == branch.Branch))
		//	//		{
		//	//			commit.SetDim();
		//	//		}
		//	//	}

		//	//}
		//	//else
		//	//{
		//	//	// Normal branches and main branches
		//	//	foreach (CommitViewModel commit in Commits)
		//	//	{
		//	//		if (commit.Commit.Branch.Id != branch.Branch.Id)
		//	//		{
		//	//			commit.SetDim();
		//	//		}
		//	//	}
		//	//}
		//}


		//public void MouseLeaveBranch(BranchViewModel branch)
		//{
		//	branch.SetNormal();

		//	//foreach (CommitViewModel commit in Commits)
		//	//{
		//	//	commit.SetNormal(viewModelService.GetSubjectBrush(commit.Commit));
		//	//}
		//}


		private void UpdateViewModel()
		{
			Timing t = new Timing();

			if (!IsInFilterMode())
			{
				UpdateViewModelImpl();

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

			UpdateViewModelImpl();

			t.Log("Updated repository view model");
		}


		private void UpdateViewModelImpl()
		{
			NotifyAll();

			VirtualItemsSource.DataChanged();
		}


		public int SelectedIndex
		{
			get { return Get(); }
			set { Set(value); }
		}


		public object SelectedItem
		{
			get { return Get().Value; }
			set
			{
				Set(value);
			}
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
			int offsetY = Converters.ToY(rows);
			Canvas.Offset = new Point(Canvas.Offset.X, Math.Max(Canvas.Offset.Y - offsetY, 0));
		}


		private void ScrollTo(int rows)
		{
			int offsetY = Converters.ToY(rows);
			Canvas.Offset = new Point(Canvas.Offset.X, Math.Max(offsetY, 0));
		}


		public void ShowUncommittedDetails()
		{
			SelectedIndex = 0;
			ScrollTo(0);
			IsShowCommitDetails = true;
		}


		public void Clicked(Point position)
		{
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
	}
}