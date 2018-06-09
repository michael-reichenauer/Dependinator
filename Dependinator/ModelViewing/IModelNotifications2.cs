using System.Threading.Tasks;


namespace Dependinator.ModelViewing
{
	internal interface IModelNotifications
	{
		Task ManualRefreshAsync(bool refreshLayout = false);
	}
}