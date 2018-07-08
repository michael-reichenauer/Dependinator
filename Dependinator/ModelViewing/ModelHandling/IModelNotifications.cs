using System;
using System.Threading.Tasks;


namespace Dependinator.ModelViewing.ModelHandling
{
	internal interface IModelNotifications
	{
		event EventHandler ModelUpdated;
		Task ManualRefreshAsync(bool refreshLayout = false);
	}
}