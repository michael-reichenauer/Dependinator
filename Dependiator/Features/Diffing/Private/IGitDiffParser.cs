using System.Threading.Tasks;
using Dependiator.Common;
using Dependiator.Git;


namespace Dependiator.Features.Diffing.Private
{
	internal interface IGitDiffParser
	{
		Task<CommitDiff> ParseAsync(CommitSha commitSha, string patch, bool addPrefixes = true);
	}
}