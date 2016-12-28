using System.Collections.Generic;
using System.Threading.Tasks;
using Dependiator.Common;


namespace Dependiator.GitModel
{
	internal interface ICommitsFiles
	{
		Task<IEnumerable<CommitFile>> GetAsync(CommitSha commitSha);
	}
}