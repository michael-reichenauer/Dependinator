using System.Collections.Generic;
using Dependiator.Git;


namespace Dependiator.GitModel.Private
{
	internal interface ICommitBranchNameService
	{
		BranchName GetBranchName(MCommit commit);

		void SetMasterBranchCommits(MRepository repository);

		void SetBranchTipCommitsNames(MRepository repository);

		void SetSpecifiedCommitBranchNames(
			IReadOnlyList<CommitBranchName> specifiedNames, MRepository repository);

		void SetCommitBranchNames(
			IReadOnlyList<CommitBranchName> commitBranches, MRepository repository);

		void SetNeighborCommitNames(MRepository repository);
	}
}
