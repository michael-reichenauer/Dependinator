using System.Threading.Tasks;
using Dependiator.Common;
using Dependiator.Git;
using Dependiator.Utils;


namespace Dependiator.Features.Branches.Private
{
	internal interface IGitBranchService
	{
		Task<R> CreateBranchAsync(BranchName branchName, CommitSha commitSha);

		Task<R> SwitchToBranchAsync(BranchName branchName);

		Task<R<BranchName>> SwitchToCommitAsync(CommitSha commitSha, BranchName branchName);

		Task<R> MergeCurrentBranchFastForwardOnlyAsync();

		Task<R> MergeCurrentBranchAsync();

		Task<R> MergeAsync(BranchName branchName);

		R<GitDivergence> CheckAheadBehind(CommitSha localTip, CommitSha remoteTip);

		Task<R> DeleteLocalBranchAsync(BranchName branchName);
	}
}