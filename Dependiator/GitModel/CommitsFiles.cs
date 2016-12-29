//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//using Dependiator.Common;
//using Dependiator.Utils;


//namespace Dependiator.GitModel
//{
//	[SingleInstance]
//	internal class CommitsFiles : ICommitsFiles
//	{

//		public async Task<IEnumerable<CommitFile>> GetAsync(CommitSha commitSha)
//		{
//			await Task.Yield();
//			// This commit id is no longer relevant 
//			return Enumerable.Empty<CommitFile>();
//		}
//	}
//}