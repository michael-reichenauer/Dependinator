using System.Threading.Tasks;


namespace Dependinator.ModelViewing
{
	internal interface IModelNotifications2
	{
		Task ManualRefreshAsync(bool refreshLayout = false);
	}
}