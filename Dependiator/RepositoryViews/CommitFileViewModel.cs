using System.Windows.Media;
using Dependiator.Common;
using Dependiator.Common.ThemeHandling;
using Dependiator.Git;
using Dependiator.GitModel;
using Dependiator.Utils.UI;


namespace Dependiator.RepositoryViews
{
	internal class CommitFileViewModel : ViewModel
	{
		private readonly IThemeService themeService;

		private readonly CommitFile file;

		public CommitFileViewModel(
			IThemeService themeService,
			CommitFile file)
		{
			this.themeService = themeService;
			this.file = file;
		}


		public CommitSha Id { get; set; }

		public string WorkingFolder { get; set; }

		public string Name
		{
			get { return Get(); }
			set { Set(value); }
		}

		public string Status
		{
			get { return Get(); }
			set { Set(value); }
		}

		public bool HasConflicts => file.Status.HasFlag(GitFileStatus.Conflict);
		public bool HasNotConflicts => !HasConflicts;
		//public bool IsMerged => diffService.IsMerged(WorkingFolder, file);
		//public bool IsDeleted => diffService.IsDeleted(WorkingFolder, file);
		//public bool IsUseBase => diffService.IsUseBase(WorkingFolder, file);
		//public bool IsUseYours => diffService.IsUseYours(WorkingFolder, file);
		//public bool IsUseTheirs => diffService.IsUseTheirs(WorkingFolder, file);

		public Brush FileNameBrush => file.Status != GitFileStatus.Conflict
			? themeService.Theme.TextBrush : themeService.Theme.ConflictBrush;

		public bool IsUncommitted => HasNotConflicts && Id == CommitSha.Uncommitted;
	}
}