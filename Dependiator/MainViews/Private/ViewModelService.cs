using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Dependiator.Common;
using Dependiator.Common.ThemeHandling;
using Dependiator.GitModel;
using Dependiator.Utils;


namespace Dependiator.MainViews.Private
{
	/// <summary>
	/// ViewModelService
	/// </summary>
	[SingleInstance]
	internal class ViewModelService : IViewModelService
	{
		private static readonly int CommitHeight = Converters.ToY(1);

		private readonly IThemeService themeService;
		private readonly IRepositoryMgr repositoryMgr;
		private readonly IRepositoryCommands repositoryCommands;

		private readonly Func<BranchViewModel> branchViewModelProvider;

		private bool isFirstTime = true;

		public ViewModelService(
			IThemeService themeService,
			IRepositoryMgr repositoryMgr,
			IRepositoryCommands repositoryCommands,
			Func<BranchViewModel> branchViewModelProvider)
		{
			this.themeService = themeService;
			this.repositoryMgr = repositoryMgr;
			this.repositoryCommands = repositoryCommands;
			this.branchViewModelProvider = branchViewModelProvider;
		}

		public void UpdateViewModel(MainViewModel mainViewModel)
		{
			Timing t = new Timing();

			List<Branch> specifiedBranches = new List<Branch>();

			foreach (BranchName name in mainViewModel.SpecifiedBranchNames)
			{
				Branch branch;

				// First try find active branch with name and then other branch
				if (name != null)
				{
					branch = repositoryMgr.Repository.Branches
						.FirstOrDefault(b => b.Name == name && b.IsActive)
									 ?? repositoryMgr.Repository.Branches.FirstOrDefault(b => b.Name == name);
					if (branch != null && !specifiedBranches.Any(b => b.Name == name))
					{
						specifiedBranches.Add(branch);
					}
				}
				else
				{
					branch = repositoryMgr.Repository.Branches.First(b => b.IsCurrentBranch);
					if (branch != null && !specifiedBranches.Any(b => b.Name == branch.Name))
					{
						specifiedBranches.Add(branch);
					}
				}
			}

			if (!specifiedBranches.Any())
			{
				Branch currentBranch = repositoryMgr.Repository.CurrentBranch;

				specifiedBranches.Add(currentBranch);
			}

			if (isFirstTime && !repositoryMgr.Repository.MRepository.IsCached)
			{
				Branch currentBranch = repositoryMgr.Repository.CurrentBranch;
				isFirstTime = false;
				if (!specifiedBranches.Any(b => b == currentBranch))
				{
					specifiedBranches.Add(currentBranch);
				}
			}

			IReadOnlyList<Branch> branches = GetBranchesIncludingParents(specifiedBranches);

			List<Commit> commits = GetCommits(branches);

			mainViewModel.ShownBranches.Clear();
			branches
				.OrderBy(b => b.Name)
				.ForEach(b => mainViewModel.ShownBranches.Add(
					new BranchItem(b, mainViewModel.ShowBranchCommand, null)));

			mainViewModel.HidableBranches.Clear();
			branches
				.Where(b => b.Name != BranchName.Master)
				.OrderBy(b => b.Name)
				.ForEach(b => mainViewModel.HidableBranches.Add(
					new BranchItem(b, mainViewModel.ShowBranchCommand, null)));

			mainViewModel.ShowableBranches.Clear();
			IEnumerable<Branch> showableBranches = repositoryMgr.Repository.Branches
				.Where(b => b.IsActive);
			IReadOnlyList<BranchItem> showableBrancheItems = BranchItem.GetBranches(
				showableBranches,
				mainViewModel.ShowBranchCommand);
			showableBrancheItems.ForEach(b => mainViewModel.ShowableBranches.Add(b));

			mainViewModel.DeletableBranches.Clear();
					

			UpdateViewModel(mainViewModel, branches, commits);

			t.Log("Updated view model");
		}


		private void UpdateViewModel(
			MainViewModel mainViewModel,
			IReadOnlyList<Branch> branches,
			List<Commit> commits)
		{
			UpdateBranches(branches, commits, mainViewModel);

			UpdateCommits(commits, mainViewModel);

			UpdateMerges(branches, mainViewModel);

			mainViewModel.SpecifiedBranches = branches.ToList();
			mainViewModel.SpecifiedBranchNames = new BranchName[0];
		}


		public int ToggleMergePoint(MainViewModel maninViewModel, Commit commit)
		{
			List<Branch> currentlyShownBranches = maninViewModel.SpecifiedBranches.ToList();

			BranchViewModel clickedBranch = maninViewModel
				.Branches.First(b => b.Branch == commit.Branch);

			Commit stableCommit = commit;
			if (commit.HasSecondParent && !currentlyShownBranches.Contains(commit.SecondParent.Branch))
			{
				// Showing the specified branch
				Log.Usage("Open branch");
				Log.Info($"Open branch {commit.SecondParent.Branch}");
				currentlyShownBranches.Add(commit.SecondParent.Branch);
				if (commit.SecondParent.Branch.IsMainPart)
				{
					currentlyShownBranches.Add(commit.SecondParent.Branch.LocalSubBranch);
				}
			}
			else
			{
				// Closing shown branch
				BranchViewModel otherBranch;

				if (commit.HasSecondParent
					&& commit.SecondParent.Branch != commit.Branch
					&& currentlyShownBranches.Contains(commit.SecondParent.Branch))
				{
					otherBranch = maninViewModel.Branches
						.First(b => b.Branch == commit.SecondParent.Branch);

					if (clickedBranch.BranchColumn > otherBranch.BranchColumn)
					{
						// Closing the branch that was clicked on since that is to the right
						otherBranch = clickedBranch;
						stableCommit = commit.SecondParent;
					}
				}
				else if (!commit.HasFirstChild || 
					commit.Branch.TipCommit == commit)
				{
					// A branch tip, closing the clicked branch
					otherBranch = clickedBranch;
					stableCommit = commit.Branch.ParentCommit;
					if (clickedBranch.Branch.IsLocalPart)
					{
						otherBranch = maninViewModel
							.Branches.First(b => b.Branch == clickedBranch.Branch.MainbBranch);
						stableCommit = otherBranch.Branch.ParentCommit;
					}
				}
				else
				{
					otherBranch = maninViewModel.Branches
						.First(b => b.Branch == commit.FirstParent.Branch);

					if (clickedBranch.BranchColumn > otherBranch.BranchColumn)
					{
						// Closing the branch that was clicked on since that is to the right
						otherBranch = clickedBranch;
						stableCommit = commit.FirstParent;
					}
				}

				if (otherBranch.Branch.IsLocalPart)
				{
					otherBranch = maninViewModel.Branches
						.First(b => b.Branch == otherBranch.Branch.MainbBranch);
					stableCommit = otherBranch.Branch.ParentCommit;
				}

				Log.Usage("Close branch");
				Log.Info($"Close branch {otherBranch.Branch}");
				IEnumerable<Branch> closingBranches = GetBranchAndDescendants(
					currentlyShownBranches, otherBranch.Branch);

				currentlyShownBranches.RemoveAll(b => b.Name != BranchName.Master && closingBranches.Contains(b));
			}

			CommitViewModel stableCommitViewModel = maninViewModel.CommitsById[stableCommit.Id];

			int currentRow = stableCommitViewModel.RowIndex;
			maninViewModel.SelectedIndex = currentRow;
			maninViewModel.SpecifiedBranches = currentlyShownBranches;
			UpdateViewModel(maninViewModel);

			CommitViewModel newCommitViewModel = maninViewModel.CommitsById[stableCommit.Id];

			int newRow = newCommitViewModel.RowIndex;
			Log.Debug($"Row {currentRow}->{newRow} for {stableCommit}");

			return currentRow - newRow;
		}


		public void ShowBranch(MainViewModel maninViewModel, Branch branch)
		{
			List<Branch> currentlyShownBranches = maninViewModel.SpecifiedBranches.ToList();

			bool isShowing = currentlyShownBranches.Contains(branch);

			if (!isShowing)
			{
				// Showing the specified branch
				currentlyShownBranches.Add(branch);
				if (branch.IsMainPart)
				{
					currentlyShownBranches.Add(branch.LocalSubBranch);
				}

				maninViewModel.SpecifiedBranches = currentlyShownBranches;
				UpdateViewModel(maninViewModel);
			}

			var x = maninViewModel.Branches.FirstOrDefault(b => b.Branch == branch);
			if (x != null)
			{
				var y = x.TipRowIndex;
				maninViewModel.ScrollRows(maninViewModel.Commits.Count);
				maninViewModel.ScrollRows(-(y - 10));
			}

			maninViewModel.VirtualItemsSource.DataChanged(maninViewModel.Width);
		}

		public void HideBranch(MainViewModel maninViewModel, Branch branch)
		{
			List<Branch> currentlyShownBranches = maninViewModel.SpecifiedBranches.ToList();

			bool isShowing = currentlyShownBranches.Contains(branch);

			if (isShowing)
			{
				IEnumerable<Branch> closingBranches = GetBranchAndDescendants(
					currentlyShownBranches, branch);

				if (branch.IsLocalPart)
				{
					closingBranches = closingBranches.Concat(GetBranchAndDescendants(
						currentlyShownBranches,
						currentlyShownBranches.First(b => b.LocalSubBranch == branch)));
					;
				}

				currentlyShownBranches.RemoveAll(b => b.Name != BranchName.Master && closingBranches.Contains(b));

				maninViewModel.SpecifiedBranches = currentlyShownBranches;
				UpdateViewModel(maninViewModel);

				maninViewModel.VirtualItemsSource.DataChanged(maninViewModel.Width);
			}
		}


		public async Task SetFilterAsync(MainViewModel maninViewModel, string filterText)
		{
			if (string.IsNullOrEmpty(filterText))
			{
				List<Branch> preFilterBranches = maninViewModel.PreFilterBranches.ToList();
				CommitViewModel preFilterSelectedItem = maninViewModel.PreFilterSelectedItem;
				maninViewModel.PreFilterBranches = null;
				maninViewModel.PreFilterSelectedItem = null;

				var commit = maninViewModel.SelectedItem as CommitViewModel;
				if (commit != null && !preFilterBranches.Contains(commit.Commit.Branch))
				{
					preFilterBranches.Add(commit.Commit.Branch);
				}
				else if (commit == null)
				{
					maninViewModel.SelectedItem = preFilterSelectedItem;
				}

				maninViewModel.SpecifiedBranches = preFilterBranches;
				UpdateViewModel(maninViewModel);
			}
			else
			{
				Timing t = new Timing();
				List<Commit> commits = await GetFilteredCommitsAsync(filterText);
				t.Log($"Got filtered {commits.Count} commits");

				if (maninViewModel.PreFilterBranches == null)
				{
					// Storing pre-filter mode state to be used when leaving filter mode
					maninViewModel.PreFilterBranches = maninViewModel.SpecifiedBranches;
					maninViewModel.PreFilterSelectedItem = maninViewModel.SelectedItem as CommitViewModel;
					maninViewModel.PreFilterSelectedItem = null;
				}

				Branch[] branches = new Branch[0];
				UpdateViewModel(maninViewModel, branches, commits);
			}
		}

		private Task<List<Commit>> GetFilteredCommitsAsync(string filterText)
		{
			bool isSearchSpecifiedNames = filterText == "$gm:";
			bool isSearchCommitNames = filterText == "$gm:c";

			Repository repository = repositoryMgr.Repository;

			IEnumerable<Commit> commits = repository.Commits.Values;

			Log.Debug($"Searching in {commits.Count()} commits");

			return Task.Run(() =>
			{
				return commits
					.Where(c =>
						StartsWith(c.RealCommitSha.Sha, filterText)
						|| Contains(c.Subject, filterText)
						|| Contains(c.Author, filterText)
						|| Contains(c.AuthorDateText, filterText)
						|| Contains(c.Tickets, filterText)
						|| Contains(c.Tags, filterText)
						|| Contains(c.Branch.Name, filterText)
						|| (isSearchSpecifiedNames && !string.IsNullOrEmpty(c.SpecifiedBranchName))
						|| (isSearchCommitNames && !string.IsNullOrEmpty(c.CommitBranchName)))
					.OrderByDescending(c => c.CommitDate)
					.ToList();
			});
		}


		private static bool Contains(string text, string subText)
		{
			if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(subText))
			{
				return false;
			}

			return text.IndexOf(subText, StringComparison.OrdinalIgnoreCase) != -1;
		}

		private static bool StartsWith(string text, string subText)
		{
			if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(subText))
			{
				return false;
			}

			return text.StartsWith(subText, StringComparison.OrdinalIgnoreCase);
		}


		private IReadOnlyList<Branch> GetBranchesIncludingParents(IEnumerable<Branch> branches)
		{
			Branch masterBranch = GetMasterBranch(repositoryMgr.Repository);
			Branch[] masterBranches = { masterBranch };

			List<Branch> branchesInRepo = new List<Branch>();
			foreach (Branch branch in branches.Where(b => b != null))
			{
				Branch branchInRepo = repositoryMgr.Repository.Branches
					.FirstOrDefault(b => b.Id == branch.Id);

				if (branchInRepo != null)
				{
					branchesInRepo.Add(branchInRepo);
					if (branchInRepo.IsMainPart)
					{
						branchesInRepo.Add(branchInRepo.LocalSubBranch);
					}
					else if (branchInRepo.IsLocalPart)
					{
						branchesInRepo.Add(branchInRepo.MainbBranch);
					}
				}
			}

			List<Branch> branchesWithParents = masterBranches.
				Concat(branchesInRepo
					.Concat(branchesInRepo.SelectMany(branch => branch.Parents().Take(10))))
				.Distinct()
				.OrderBy(b => b, Compare.With<Branch>(CompareBranches))
				.ToList();

			return branchesWithParents;
		}

		private static int CompareBranches(Branch x, Branch y)
		{
			if (HasAncestor(y, x))
			{
				return -1;
			}
			else if (HasAncestor(x, y))
			{
				return 1;
			}

			return 0;
		}


		public static bool HasAncestor(Branch branch, Branch ancestor)
		{
			if (branch.IsLocalPart && branch.MainbBranch == ancestor)
			{
				return true;
			}

			Branch current = branch;

			while (current.HasParentBranch)
			{
				if (current.ParentBranch == ancestor)
				{
					return true;
				}

				current = current.ParentBranch;
			}

			return false;
		}


		private static IEnumerable<Branch> GetBranchAndDescendants(
		IEnumerable<Branch> branches, Branch branch)
		{
			IEnumerable<Branch> children = branches
				.Where(b => b.HasParentBranch && b.ParentBranch == branch || b.MainBranchId == branch.Id);

			return
				new[] { branch }.Concat(children.SelectMany(b => GetBranchAndDescendants(branches, b)));
		}


		private static List<Commit> GetCommits(IEnumerable<Branch> branches)
		{
			return branches
				.SelectMany(branch => branch.Commits)
				.OrderByDescending(commit => commit.CommitDate)
				.ToList();
		}


		private void UpdateCommits(
			IReadOnlyList<Commit> sourceCommits,
			MainViewModel maninViewModel)
		{
			List<CommitViewModel> commits = maninViewModel.Commits;
			var commitsById = maninViewModel.CommitsById;

			SetNumberOfItems(
				commits, 
				sourceCommits.Count,
				i => new CommitViewModel(themeService, repositoryCommands));

			commitsById.Clear();
			int graphWidth = maninViewModel.GraphWidth;

			int index = 0;
			foreach (Commit commit in sourceCommits)
			{
				CommitViewModel commitViewModel = commits[index];
				commitsById[commit.Id] = commitViewModel;

				commitViewModel.Commit = commit;
				commitViewModel.RowIndex = index++;

				commitViewModel.BranchViewModel = GetBranchViewModel(maninViewModel, commit.Branch);

				int x = commitViewModel.BranchViewModel?.X ?? -20;
				int y = Converters.ToY(commitViewModel.RowIndex);

				commitViewModel.XPoint = commitViewModel.IsEndPoint
					? 3 + x
					: commitViewModel.IsMergePoint ? 2 + x : 4 + x;

				commitViewModel.GraphWidth = graphWidth;
				commitViewModel.Width = maninViewModel.Width - 35;

				commitViewModel.Rect = new Rect(0, y, commitViewModel.Width, CommitHeight);

				commitViewModel.Brush = themeService.GetBranchBrush(commit.Branch);
				commitViewModel.BrushInner = commitViewModel.Brush;
				commitViewModel.SetNormal(GetSubjectBrush(commit));
				commitViewModel.BranchToolTip = GetBranchToolTip(commit.Branch);

				if (commitViewModel.IsMergePoint
					&& !commit.HasSecondParent
					&& (commit == commit.Branch.TipCommit || commit == commit.Branch.FirstCommit))
				{
					commitViewModel.BrushInner = themeService.Theme.GetDarkerBrush(commitViewModel.Brush);
				}

				commitViewModel.NotifyAll();
			}
		}


		private void UpdateBranches(
			IReadOnlyList<Branch> sourceBranches,
			List<Commit> commits,
			MainViewModel maninViewModel)
		{
			int maxColumn = 0;
			var branches = maninViewModel.Branches;

			SetNumberOfItems(branches, sourceBranches.Count, i => branchViewModelProvider());

			int index = 0;
			List<BranchViewModel> addedBranchColumns = new List<BranchViewModel>();
			foreach (Branch sourceBranch in sourceBranches)
			{
				BranchViewModel branch = branches[index++];
				branch.Branch = sourceBranch;

				branch.ActiveBranches = maninViewModel.HidableBranches;
				branch.ShownBranches = maninViewModel.ShownBranches;

				branch.TipRowIndex = commits.FindIndex(c => c == sourceBranch.TipCommit);
				branch.FirstRowIndex = commits.FindIndex(c => c == sourceBranch.FirstCommit);
				int height = Converters.ToY(branch.FirstRowIndex - branch.TipRowIndex) + 8;

				branch.BranchColumn = FindFreeBranchColumn(addedBranchColumns, branch);
				maxColumn = Math.Max(branch.BranchColumn, maxColumn);
				addedBranchColumns.Add(branch);

				branch.X = branch.Branch.IsLocalPart && branch.Branch.MainbBranch.Commits.Any()
					? Converters.ToX(branch.BranchColumn) - 10
					: Converters.ToX(branch.BranchColumn);
				
			
				branch.HoverBrush = Brushes.Transparent;
				branch.Dashes = sourceBranch.IsLocalPart ? "1" : "";

				branch.Rect = new Rect(
					branch.X + 3,
					(double)Converters.ToY(branch.TipRowIndex) + Converters.HalfRow - 6,
					10,
					height + 4);

				int top = sourceBranch == sourceBranch.Repository.CurrentBranch ? -3 : 2;
				branch.Line = $"M 4,{top} L 4,{height}";

				if (branch.FirstRowIndex == branch.TipRowIndex)
				{
					branch.Line = "";
				}

				branch.SetColor(themeService.GetBranchBrush(sourceBranch));
			

				branch.BranchToolTip = GetBranchToolTip(sourceBranch);
				branch.CurrentBranchName = repositoryMgr.Repository.CurrentBranch.Name;

				branch.SetNormal();

				branch.NotifyAll();

				if (branch.IsMultiBranch)
				{
					Log.Warn($"Multibranch: {branch.Branch} in {branch.Branch.Repository.MRepository.WorkingFolder}, can be:");
					foreach (BranchName name in branch.Branch.ChildBranchNames)
					{
						Log.Warn($"   Branch: {name}");
					}
				}
			}

			maninViewModel.GraphWidth = Converters.ToX(maxColumn + 1);
		}


		private string GetBranchToolTip(Branch branch)
		{
			string name = branch.IsMultiBranch ? "MultiBranch" : branch.Name.ToString();
			string toolTip = $"{name}";
			if (branch.IsLocalPart)
			{
				toolTip += "    (local part)";
			}

			if (branch.LocalAheadCount > 0)
			{
				toolTip += $"\nLocal branch ahead: {branch.LocalAheadCount}";
			}
			else if (branch.IsLocal || branch.IsMainPart)
			{
				toolTip += "\nLocal branch";
			}

			if (branch.RemoteAheadCount > 0)
			{
				toolTip += $"\nRemote branch ahead: {branch.RemoteAheadCount}";
			}
			else if (branch.IsRemote || branch.IsLocalPart)
			{
				toolTip += "\nRemote branch";
			}
			else if (!branch.IsRemote && !branch.IsLocalPart)
			{
				toolTip += "\n(Deleted branch)";
			}

			if (branch.ChildBranchNames.Count > 1)
			{
				toolTip += $"\n\nBranch could be one of:";
				foreach (BranchName branchName in branch.ChildBranchNames)
				{
					toolTip += $"\n   {branchName}";
				}
			}

			return toolTip;
		}


		private int FindFreeBranchColumn(List<BranchViewModel> branches, BranchViewModel branch)
		{
			int column = 0;
			if (branch.Branch.HasParentBranch)
			{
				BranchViewModel parent = branches
					.FirstOrDefault(b => b.Branch == branch.Branch.ParentBranch);
				if (parent != null)
				{
					column = parent.BranchColumn + 1;
				}
			}

			while (true)
			{
				if (branches.Any(current =>
					current.Id != branch.Id
					&& column == current.BranchColumn
					&& IsOverlapping(
						current.TipRowIndex,
						current.FirstRowIndex,
						branch.TipRowIndex - 5,
						branch.FirstRowIndex + 5)))
				{
					column++;
				}
				else
				{
					return column;
				}
			}
		}

		private static bool IsOverlapping(
			int areaTopIndex,
			int areaBottomIndex,
			int itemTopIndex,
			int ItemBottomIndex)
		{
			return
				(itemTopIndex >= areaTopIndex && itemTopIndex <= areaBottomIndex)
					|| (ItemBottomIndex >= areaTopIndex && ItemBottomIndex <= areaBottomIndex)
					|| (itemTopIndex <= areaTopIndex && ItemBottomIndex >= areaBottomIndex);
		}


		private void UpdateMerges(
			IEnumerable<Branch> sourceBranches,
			MainViewModel maninViewModel)
		{
			var branches = maninViewModel.Branches;
			var commits = maninViewModel.Commits;
			var commitsById = maninViewModel.CommitsById;
			var merges = maninViewModel.Merges;

			var mergePoints = commits
				.Where(c => c.IsMergePoint && c.Commit.HasSecondParent && sourceBranches.Contains(c.Commit.SecondParent.Branch))
				.ToList();

			var branchStarts = branches.Where(b =>
				b.Branch.HasParentBranch && sourceBranches.Contains(b.Branch.ParentCommit.Branch))
				.Select(b => b.Branch.FirstCommit)
				.ToList();

			bool isMergeInProgress = false;
	

			int mergeCount = mergePoints.Count + branchStarts.Count + (isMergeInProgress ? 1 : 0);

			SetNumberOfItems(merges, mergeCount, _ => new MergeViewModel());

			int index = 0;
			foreach (CommitViewModel childCommit in mergePoints)
			{
				CommitViewModel parentCommit = commitsById[childCommit.Commit.SecondParent.Id];

				MergeViewModel merge = merges[index++];

				SetMerge(merge, branches, childCommit, parentCommit);
			}

			foreach (Commit childCommit in branchStarts)
			{
				CommitViewModel parentCommit = commitsById[childCommit.FirstParent.Id];

				MergeViewModel merge = merges[index++];

				SetMerge(merge, branches, commitsById[childCommit.Id], parentCommit, false);
			}
		}


		private void SetMerge(
			MergeViewModel merge,
			IReadOnlyCollection<BranchViewModel> branches,
			CommitViewModel childCommit,
			CommitViewModel parentCommit,
			bool isMerge = true)
		{
			BranchViewModel childBranch = branches
				.First(b => b.Branch == childCommit.Commit.Branch);
			BranchViewModel parentBranch = branches
				.First(b => b.Branch == parentCommit.Commit.Branch);

			if (isMerge)
			{
				childCommit.BrushInner = themeService.Theme.GetDarkerBrush(childCommit.Brush);
			}

			int childRow = childCommit.RowIndex;
			int parentRow = parentCommit.RowIndex;
			int childColumn = childBranch.BranchColumn;
			int parentColumn = parentBranch.BranchColumn;

			bool isBranchStart = (childCommit.Commit.HasFirstParent
				&& childCommit.Commit.FirstParent.Branch != childCommit.Commit.Branch)
				|| (childCommit.Commit.Branch.IsLocalPart && parentCommit.Commit.Branch.IsMainPart);

			BranchViewModel mainBranch = childColumn >= parentColumn ? childBranch : parentBranch;

			int childX = childCommit.X;
			int parentX = parentCommit.X;

			int x1 = childX <= parentX ? 0 : childX - parentX - 6;
			int y1 = 0;
			int x2 = parentX <= childX ? 0 : parentX - childX - 6;
			int y2 = Converters.ToY(parentRow - childRow) + Converters.HalfRow - 8;

			if (isBranchStart && x1 != x2)
			{
				y1 = y1 + 2;
				x1 = x1 + 2;
			}

			merge.ChildRow = childRow;
			merge.ParentRow = parentRow;

			double y = (double)Converters.ToY(childRow);

			merge.Rect = new Rect(
				(double)Math.Min(childX, parentX) + 10,
				y + Converters.HalfRow,
				Math.Abs(childX - parentX) + 2 + (x1 == x2 ? 2 : 0),
				y2 + 2);
			merge.Width = merge.Rect.Width;

			merge.Line = $"M {x1},{y1} L {x2},{y2}";
			merge.Brush = mainBranch.Brush;
			merge.Stroke = isBranchStart ? 2 : 1;
			merge.StrokeDash =
				(childCommit.Commit.Branch.IsLocalPart && parentCommit.Commit.Branch.IsMainPart)
				|| (parentCommit.Commit.Branch.IsLocalPart && childCommit.Commit.Branch.IsMainPart)
				? "1" : "";

			merge.NotifyAll();
		}


		private static Branch GetMasterBranch(Repository repository)
		{
			return repository.Branches.First(b => b.Name == BranchName.Master && b.IsActive);
		}


		private BranchViewModel GetBranchViewModel(
			MainViewModel maninViewModel, Branch branch)
		{
			foreach (BranchViewModel current in maninViewModel.Branches)
			{
				if (current.Branch == branch)
				{
					return current;
				}
			}

			return null;
		}


		public Brush GetSubjectBrush(Commit commit)
		{
			Brush subjectBrush;
			if (commit.HasConflicts)
			{
				subjectBrush = themeService.Theme.ConflictBrush;
			}
			else if (commit.IsMerging)
			{
				subjectBrush = themeService.Theme.MergeBrush;
			}
			else if (commit.IsUncommitted)
			{
				subjectBrush = themeService.Theme.UnCommittedBrush;
			}
			else if (commit.IsLocalAhead && commit.Branch.LocalAheadCount > 0)
			{
				subjectBrush = themeService.Theme.LocalAheadBrush;
			}
			else if (commit.IsRemoteAhead && commit.Branch.RemoteAheadCount > 0)
			{
				subjectBrush = themeService.Theme.RemoteAheadBrush;
			}
			//else if (commit.CommitBranchName != null)
			//{
			//	subjectBrush = Brushes.Fuchsia;
			//}
			//else if (commit.SpecifiedBranchName != null)
			//{
			//	subjectBrush = Brushes.Chocolate;
			//}
			else
			{
				subjectBrush = themeService.Theme.SubjectBrush;
			}

			return subjectBrush;
		}


		private void SetNumberOfItems<T>(
			List<T> items, int count, Func<int, T> factory)
		{
			if (items.Count > count)
			{
				// To many items, lets remove the items no longer used
				items.RemoveRange(count, items.Count - count);
			}

			if (items.Count < count)
			{
				// To few items, lets create the rows needed
				for (int i = items.Count; i < count; i++)
				{
					items.Add(factory(i));
				}
			}
		}
	}
}