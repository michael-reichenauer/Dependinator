using System.Threading.Tasks;
using Dependiator.Common;
using Dependiator.Git;
using Dependiator.Utils;


namespace Dependiator.Features.Diffing.Private
{
	internal interface IGitDiffService
	{
		Task<R<CommitDiff>> GetCommitDiffAsync(CommitSha commitSha);

		Task<R<CommitDiff>> GetCommitDiffRangeAsync(CommitSha commitSha1, CommitSha commitSha2);

		Task<R<CommitDiff>> GetFileDiffAsync(CommitSha commitSha, string path);

		void GetFile(string fileId, string filePath);

		Task ResolveAsync(string path);
	}
}