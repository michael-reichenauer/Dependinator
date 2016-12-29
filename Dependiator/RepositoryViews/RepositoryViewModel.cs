using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Dependiator.ApplicationHandling;
using Dependiator.Common;
using Dependiator.Common.MessageDialogs;
using Dependiator.Common.ProgressHandling;
using Dependiator.Common.ThemeHandling;
using Dependiator.GitModel;
using Dependiator.RepositoryViews.Private;
using Dependiator.Utils;
using Dependiator.Utils.UI;
using Dependiator.Utils.UI.VirtualCanvas;
using ListBox = System.Windows.Controls.ListBox;


namespace Dependiator.RepositoryViews
{
	/// <summary>
	/// View model
	/// </summary>
	[SingleInstance]
	internal class RepositoryViewModel : ViewModel
	{
		private static readonly TimeSpan FilterDelay = TimeSpan.FromMilliseconds(300);

		private readonly IViewModelService viewModelService;
		private readonly IRepositoryService repositoryService;

		private readonly IThemeService themeService;
		private readonly WorkingFolder workingFolder;
		private readonly IProgressService progress;


		private readonly DispatcherTimer filterTriggerTimer = new DispatcherTimer();
		private string settingFilterText = "";

		private int width = 0;
		private int graphWidth = 0;

		public List<BranchViewModel> Branches { get; } = new List<BranchViewModel>();
		public List<MergeViewModel> Merges { get; } = new List<MergeViewModel>();
		public List<CommitViewModel> Commits { get; } = new List<CommitViewModel>();


		public Dictionary<CommitId, CommitViewModel> CommitsById { get; } =
			new Dictionary<CommitId, CommitViewModel>();

		private readonly AsyncLock refreshLock = new AsyncLock();


		public IReadOnlyList<Branch> SpecifiedBranches { get; set; } = new Branch[0];

		//public string WorkingFolder { get; set; }

		public IReadOnlyList<BranchName> SpecifiedBranchNames { get; set; } = new List<BranchName>();
		public ZoomableCanvas Canvas { get; set; }


		public RepositoryViewModel(
			WorkingFolder workingFolder,
			IViewModelService viewModelService,
			IRepositoryService repositoryService,
			IThemeService themeService,
			IProgressService progressService,
			Func<CommitDetailsViewModel> commitDetailsViewModelProvider)
		{
			this.workingFolder = workingFolder;
			this.viewModelService = viewModelService;
			this.repositoryService = repositoryService;

			this.themeService = themeService;
			this.progress = progressService;

			VirtualItemsSource = new RepositoryVirtualItemsSource(Branches, Merges, Commits);

			filterTriggerTimer.Tick += FilterTrigger;
			filterTriggerTimer.Interval = FilterDelay;

			CommitDetailsViewModel = commitDetailsViewModelProvider();
		}	

		public Branch MergingBranch { get; private set; }


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



		public string CurrentBranchName
		{
			get { return Get(); }
			set { Set(value).Notify(nameof(PullCurrentBranchText), nameof(PushCurrentBranchText)); }
		}

		public Brush CurrentBranchBrush
		{
			get { return Get(); }
			set { Set(value); }
		}

		public string PullCurrentBranchText => $"Update current branch '{CurrentBranchName}'";

		public string PushCurrentBranchText => $"Push current branch '{CurrentBranchName}'";


		public Command<Branch> ShowBranchCommand => Command<Branch>(ShowBranch);

		public Command<Branch> HideBranchCommand => Command<Branch>(HideBranch);

		public Command ToggleDetailsCommand => Command(ToggleCommitDetails);

		public RepositoryVirtualItemsSource VirtualItemsSource { get; }

		public ObservableCollection<BranchItem> ShowableBranches { get; }
			= new ObservableCollection<BranchItem>();

		public ObservableCollection<BranchItem> DeletableBranches { get; }
			= new ObservableCollection<BranchItem>();

		public ObservableCollection<BranchItem> HidableBranches { get; }
			= new ObservableCollection<BranchItem>();

		public ObservableCollection<BranchItem> ShownBranches { get; }
			= new ObservableCollection<BranchItem>();


		public CommitDetailsViewModel CommitDetailsViewModel { get; }

		public string FilterText { get; private set; } = "";


		public ListBox ListBox { get; set; }

		public IReadOnlyList<Branch> PreFilterBranches { get; set; }

		public CommitViewModel PreFilterSelectedItem { get; set; }

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
					Commits.ForEach(commit => commit.WindowWidth = width - 2);
					VirtualItemsSource.DataChanged(width);
				}
			}
		}

		public int GraphWidth
		{
			get { return graphWidth; }
			set
			{
				if (graphWidth != value)
				{
					graphWidth = value;
					Commits.ForEach(commit => commit.GraphWidth = graphWidth);
				}
			}
		}


		public void ShowBranch(BranchName branchName)
		{
			SpecifiedBranchNames = new[] { branchName };
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
					await repositoryService.LoadRepositoryAsync(workingFolder);
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


		public void MouseEnterBranch(BranchViewModel branch)
		{
			branch.SetHighlighted();

			if (branch.Branch.IsLocalPart)
			{
				// Local part branch, then do not dim common commits in main branch part
				foreach (CommitViewModel commit in Commits)
				{
					if (commit.Commit.Branch.Id != branch.Branch.Id
						&& !(commit.Commit.IsCommon
							&& commit.Commit.Branch.IsMainPart
							&& commit.Commit.Branch.LocalSubBranch == branch.Branch))
					{
						commit.SetDim();
					}
				}

			}
			else
			{
				// Normal branches and main branches
				foreach (CommitViewModel commit in Commits)
				{
					if (commit.Commit.Branch.Id != branch.Branch.Id)
					{
						commit.SetDim();
					}
				}
			}
		}


		public void MouseLeaveBranch(BranchViewModel branch)
		{
			branch.SetNormal();

			foreach (CommitViewModel commit in Commits)
			{
				commit.SetNormal(viewModelService.GetSubjectBrush(commit.Commit));
			}
		}


		private void UpdateViewModel()
		{
			Timing t = new Timing();

			if (!IsInFilterMode())
			{
				viewModelService.UpdateViewModel(this);

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
			
			viewModelService.UpdateViewModel(this);

			UpdateViewModelImpl();

			if (Commits.Any())
			{
				SelectedIndex = 0;
				SelectedItem = Commits.First();
			}

			t.Log("Updated repository view model");
		}


		private void UpdateViewModelImpl()
		{
			Commits.ForEach(commit => commit.WindowWidth = Width);
			CommitDetailsViewModel.NotifyAll();
			NotifyAll();

			VirtualItemsSource.DataChanged(width);
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
				CommitViewModel commit = value as CommitViewModel;
				if (commit != null)
				{
					SetCommitsDetails(commit);
				}
			}
		}



		private void SetCommitsDetails(CommitViewModel commit)
		{
			CommitDetailsViewModel.CommitViewModel = commit;
		}


		public void SetFilter(string text)
		{
			filterTriggerTimer.Stop();
			Log.Debug($"Filter: {text}");
			settingFilterText = (text ?? "").Trim();
			filterTriggerTimer.Start();
		}


		private class CommitPosition
		{
			public CommitPosition(Commit commit, int index)
			{
				Commit = commit;
				Index = index;
			}

			public Commit Commit { get; }
			public int Index { get; }
		}

		private async void FilterTrigger(object sender, EventArgs e)
		{
			filterTriggerTimer.Stop();
			string filterText = settingFilterText;
			FilterText = filterText;

			Log.Debug($"Filter triggered for: {FilterText}");

			CommitPosition commitPosition = TryGetSelectedCommitPosition();

			using (progress.ShowBusy())
			{
				await viewModelService.SetFilterAsync(this, filterText);
			}

			TrySetSelectedCommitPosition(commitPosition, true);
			CommitDetailsViewModel.NotifyAll();

			VirtualItemsSource.DataChanged(width);
		}


		private CommitPosition TryGetSelectedCommitPosition()
		{
			Commit selected = (SelectedItem as CommitViewModel)?.Commit;
			int index = -1;

			if (selected != null)
			{
				index = Commits.FindIndex(c => c.Commit.Id == selected.Id);
			}

			if (selected != null && index != -1)
			{
				return new CommitPosition(selected, index);
			}

			return null;
		}


		private void TrySetSelectedCommitPosition(
			CommitPosition commitPosition, bool ignoreTopIndex = false)
		{
			if (commitPosition != null)
			{
				if (!ignoreTopIndex && commitPosition.Index == 0)
				{
					// The index was 0 (top) lest ensure the index remains 0 again
					Log.Debug("Scroll to 0 since first position was 0");
					ScrollTo(0);
					if (Commits.Any())
					{
						SelectedIndex = 0;
						SelectedItem = Commits.First();
					}

					return;
				}

				Commit selected = commitPosition.Commit;

				int indexAfter = Commits.FindIndex(c => c.Commit.Id == selected.Id);

				if (selected != null && indexAfter != -1)
				{
					int indexBefore = commitPosition.Index;
					ScrollRows(indexBefore - indexAfter);
					SelectedIndex = indexAfter;
					SelectedItem = Commits[indexAfter];
					return;
				}
			}

			ScrollTo(0);
			if (Commits.Any())
			{
				SelectedIndex = 0;
				SelectedItem = Commits.First();
			}
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


		public void ShowBranch(Branch branch)
		{
			viewModelService.ShowBranch(this, branch);
		}

		public void HideBranch(Branch branch)
		{
			viewModelService.HideBranch(this, branch);
		}

		public void ShowUncommittedDetails()
		{
			SelectedIndex = 0;
			ScrollTo(0);
			IsShowCommitDetails = true;
		}

		public void ShowCurrentBranch()
		{
			viewModelService.ShowBranch(this, repositoryService.Repository.CurrentBranch);
		}


		public void Clicked(Point position)
		{
			double clickX = position.X - 9;
			double clickY = position.Y - 5;

			int row = Converters.ToRow(clickY);

			if (row < 0 || row >= Commits.Count - 1 || clickX < 0 || clickX >= graphWidth)
			{
				// Click is not within supported area.
				return;
			}

			CommitViewModel commitViewModel = Commits[row];
			int xDotCenter = commitViewModel.X;
			int yDotCenter = commitViewModel.Y;

			double absx = Math.Abs(xDotCenter - clickX);
			double absy = Math.Abs(yDotCenter - clickY);

			if ((absx < 10) && (absy < 10))
			{
				Clicked(commitViewModel);
			}
		}

		private void Clicked(CommitViewModel commitViewModel)
		{
			if (commitViewModel.IsMergePoint)
			{
				// User clicked on a merge point (toggle between expanded and collapsed)
				int rowsChange = viewModelService.ToggleMergePoint(this, commitViewModel.Commit);

				ScrollRows(rowsChange);
				VirtualItemsSource.DataChanged(width);
			}
		}
	}
}