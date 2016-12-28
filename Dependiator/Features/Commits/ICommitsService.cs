using System.Threading.Tasks;
using Dependiator.GitModel;


namespace Dependiator.Features.Commits
{
	internal interface ICommitsService
	{
		Task UndoUncommittedFileAsync(string path);
		Task CommitChangesAsync();
		Task UnCommitAsync(Commit commit);
		bool CanUnCommit(Commit commit);

		Task EditCommitBranchAsync(Commit commit);

		Task UndoUncommittedChangesAsync();

		Task CleanWorkingFolderAsync();
	}
}