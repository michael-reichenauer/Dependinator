using Dependiator.Git;


namespace Dependiator.GitModel.Private
{
	internal interface IBranchService
	{
		void AddActiveBranches(GitRepository gitRepository, MRepository repository);

		void AddInactiveBranches(MRepository repository);

		void AddMissingInactiveBranches(MRepository repository);

		void AddMultiBranches(MRepository repository);
	}
}