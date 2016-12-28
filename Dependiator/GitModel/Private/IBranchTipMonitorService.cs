using System.Threading.Tasks;


namespace Dependiator.GitModel.Private
{
	internal interface IBranchTipMonitorService
	{
		Task CheckAsync(Repository repository);
	}
}