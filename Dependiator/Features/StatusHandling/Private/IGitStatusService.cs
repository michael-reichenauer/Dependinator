using System.Collections.Generic;
using System.Threading.Tasks;
using Dependiator.Utils;


namespace Dependiator.Features.StatusHandling.Private
{
	internal interface IGitStatusService
	{
		Task<R<Status>> GetCurrentStatusAsync();

		Task<R<IReadOnlyList<string>>> GetBrancheIdsAsync();
		R<Status> GetCurrentStatus();
		R<IReadOnlyList<string>> GetBrancheIds();
	}
}