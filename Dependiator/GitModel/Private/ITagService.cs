using Dependiator.Git;


namespace Dependiator.GitModel.Private
{
	internal interface ITagService
	{
		void AddTags(GitRepository repo, MRepository repository);
	}
}