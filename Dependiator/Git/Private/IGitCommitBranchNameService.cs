using System.Collections.Generic;
using System.Threading.Tasks;
using Dependiator.Common;
using Dependiator.Utils;


namespace Dependiator.Git.Private
{
	internal interface IGitCommitBranchNameService
	{
		Task EditCommitBranchNameAsync(CommitSha commitSha, CommitSha rootSha, BranchName branchName);
		Task SetCommitBranchNameAsync(CommitSha commitSha, BranchName branchName);
		IReadOnlyList<CommitBranchName> GetEditedBranchNames(CommitSha rootSha);
		IReadOnlyList<CommitBranchName> GetCommitBrancheNames(CommitSha rootId);

		Task PushNotesAsync(CommitSha rootId);

		Task<R> FetchAllNotesAsync();
	}
}