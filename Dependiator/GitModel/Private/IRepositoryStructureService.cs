using System.Collections.Generic;
using System.Threading.Tasks;
using Dependiator.Features.StatusHandling;


namespace Dependiator.GitModel.Private
{
	internal interface IRepositoryStructureService
	{
		//Task<MRepository> GetAsync(string workingFolder);
		Task<MRepository> UpdateAsync(MRepository mRepository, Status status, IReadOnlyList<string> branchIds);
		//Task<MRepository> UpdateStatusAsync(MRepository mRepository, Status status);
		//Task<MRepository> UpdateRepoAsync(MRepository mRepository, Status status);
	}
}