using System.Collections.Generic;
using System.Threading.Tasks;
using Dependiator.Common;
using Dependiator.Features.StatusHandling;
using Dependiator.GitModel;
using Dependiator.GitModel.Private;
using Dependiator.Utils;


namespace Dependiator.Git
{
	internal interface IGitCommitsService
	{
		Task<R<IReadOnlyList<StatusFile>>> GetFilesForCommitAsync(CommitSha commitSha);

		Task EditCommitBranchAsync(CommitSha commitSha, CommitSha rootSha, BranchName branchName);
	
		IReadOnlyList<CommitBranchName> GetSpecifiedNames(CommitSha rootSha);
		IReadOnlyList<CommitBranchName> GetCommitBranches(CommitSha rootSha);

	
		Task<R<GitCommit>> CommitAsync(string message, string branchName, IReadOnlyList<CommitFile> paths);

		R<string> GetFullMessage(CommitSha commitSha);


		Task<R<IReadOnlyList<string>>> CleanWorkingFolderAsync();

		Task UndoFileInWorkingFolderAsync(string path);

		Task UndoWorkingFolderAsync();
		Task<R> ResetMerge();
		Task<R> UnCommitAsync();
	}
}