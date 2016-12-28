using Dependiator.Git;


namespace Dependiator.GitModel.Private
{
	internal interface ICommitsService
	{
		void AddBranchCommits(GitRepository gitRepository, MRepository repository);
	}
}