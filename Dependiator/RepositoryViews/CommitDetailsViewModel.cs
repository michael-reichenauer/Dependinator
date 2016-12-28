using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Dependiator.Common;
using Dependiator.Common.ThemeHandling;
using Dependiator.Features.Commits;
using Dependiator.Features.Diffing;
using Dependiator.Git;
using Dependiator.GitModel;
using Dependiator.Utils.UI;
using Dependiator.Utils;


namespace Dependiator.RepositoryViews
{
	internal class CommitDetailsViewModel : ViewModel
	{
		private readonly IDiffService diffService;
		private readonly IThemeService themeService;
		private readonly ICommitsService commitsService;
		private readonly IGitCommitsService gitCommitsService;

		private readonly ObservableCollection<CommitFileViewModel> files =
			new ObservableCollection<CommitFileViewModel>();

		private CommitId filesCommitId = null;
		private CommitViewModel commitViewModel;


		public CommitDetailsViewModel(
			IDiffService diffService,
			IThemeService themeService,
			ICommitsService commitsService,
			IGitCommitsService gitCommitsService)
		{
			this.diffService = diffService;
			this.themeService = themeService;
			this.commitsService = commitsService;
			this.gitCommitsService = gitCommitsService;
		}


		public CommitViewModel CommitViewModel
		{
			get { return commitViewModel; }
			set
			{
				if (value != commitViewModel)
				{
					commitViewModel = value;
					NotifyAll();
				}

				NotifyAll();
			}
		}

		public ObservableCollection<CommitFileViewModel> Files
		{
			get
			{
				SetFiles();

				return files;
			}
		}


		private void SetFiles()
		{
			if (CommitViewModel != null)
			{
				if (filesCommitId != CommitViewModel.Commit.RealCommitId
					|| filesCommitId == Common.CommitId.Uncommitted)
				{
					files.Clear();
					filesCommitId = CommitViewModel.Commit.RealCommitId;
					SetFilesAsync(commitViewModel.Commit).RunInBackground();
				}
			}
			else
			{
				files.Clear();
				filesCommitId = null;
			}
		}


		public string Subject
		{
			get
			{
				string subject = CommitViewModel?.Subject;
				if (CommitViewModel != null)
				{
					CommitSha commitSha = CommitViewModel.Commit.RealCommitSha;
					subject = gitCommitsService.GetFullMessage(commitSha)
						.Or(CommitViewModel?.Subject);
				}

				return subject;
			}
		}

		public string CommitId => CommitViewModel?.Commit.RealCommitSha.Sha;
		public string ShortId => CommitViewModel?.ShortId;
		public string BranchName => CommitViewModel?.Commit?.Branch?.Name;
		public FontStyle BranchNameStyle => !string.IsNullOrEmpty(SpecifiedBranchName)
			? FontStyles.Oblique : FontStyles.Normal;
		public string BranchNameUnderline => !string.IsNullOrEmpty(SpecifiedBranchName) ? "Underline" : "None";
		public string BranchNameToolTip => SpecifiedBranchName != null ? "Manually specified branch" : null;
		public string SpecifiedBranchName => CommitViewModel?.Commit?.SpecifiedBranchName;
		public Brush BranchBrush => CommitViewModel?.Brush;
		public Brush SubjectBrush => CommitViewModel?.SubjectBrush;
		public FontStyle SubjectStyle => FontStyles.Normal;
		public string Tags => CommitViewModel?.Tags;
		public string Tickets => CommitViewModel?.Tickets;
		public string BranchTips => CommitViewModel?.BranchTips;

		public Command EditBranchCommand => CommitViewModel.SetCommitBranchCommand;
		public Command<string> UndoUncommittedFileCommand => Command<string>(
			path => commitsService.UndoUncommittedFileAsync(path));
		public Command ShowCommitDiffCommand => CommitViewModel?.ShowCommitDiffCommand;

		public override string ToString() => $"{CommitId} {Subject}";

		private async Task SetFilesAsync(Commit commit)
		{
			IEnumerable<CommitFile> commitFiles = await commit.FilesTask;
			if (filesCommitId == commit.RealCommitId)
			{
				files.Clear();
				commitFiles
					.OrderBy(f => f.Status, Comparer<GitFileStatus>.Create(Compare))
					.ThenBy(f => f.Path)
					.ForEach(f => files.Add(
						new CommitFileViewModel(diffService, themeService, f, UndoUncommittedFileCommand)
						{
							Id = commit.RealCommitSha,
							Name = f.Path,
							Status = f.StatusText,
							WorkingFolder = commit.WorkingFolder
						}));
			}
		}


		private static int Compare(GitFileStatus s1, GitFileStatus s2)
		{
			if (s1 == GitFileStatus.Conflict && s2 != GitFileStatus.Conflict)
			{
				return -1;
			}
			else if (s2 == GitFileStatus.Conflict && s1 != GitFileStatus.Conflict)
			{
				return 1;
			}
			else
			{
				return 0;
			}
		}
	}
}