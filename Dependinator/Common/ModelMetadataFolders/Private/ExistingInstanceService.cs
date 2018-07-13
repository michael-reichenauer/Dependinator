using System;
using System.Threading;
using Dependinator.Api;
using Dependinator.Api.ApiHandling;
using Dependinator.Utils;
using Dependinator.Utils.Dependencies;
using Dependinator.Utils.Threading;
using DependinatorApi;
using DependinatorApi.ApiHandling;


namespace Dependinator.Common.ModelMetadataFolders.Private
{
	[SingleInstance]
	internal class ExistingInstanceService : IExistingInstanceService
	{
		private readonly IApiManagerService apiManagerService;
		

		public ExistingInstanceService(IApiManagerService apiManagerService)
		{
			this.apiManagerService = apiManagerService;
		}


		public bool TryActivateExistingInstance(string[] args)
		{
			try
			{
				string serverName = apiManagerService.GetCurrentInstanceServerName();

				if (ApiIpcClient.IsServerRegistered(serverName))
				{
					using (ApiIpcClient apiIpcClient = new ApiIpcClient(serverName))
					{
						IDependinatorApi dependinatorApi = apiIpcClient.Service<IDependinatorApi>();
						dependinatorApi.Activate(args);
						Log.Debug($"Call Activate on: {serverName}");
					}

					return true;
				}
			}
			catch (Exception e)
			{
				Log.Exception(e, "Failed to activate other instance");
			}

			return false;
		}


		public bool WaitForOtherInstance()
		{
			Timing t = Timing.Start();
			while (t.Elapsed < TimeSpan.FromSeconds(20))
			{
				try
				{
					string serverName = apiManagerService.GetCurrentInstanceServerName();

					if (!ApiIpcClient.IsServerRegistered(serverName))
					{
						Log.Debug("Other instance has closed");
						return true;
					}
				}
				catch (Exception e)
				{
					Log.Exception(e, "Failed to check if other instance is running");
				}

				Thread.Sleep(100);
			}

			Log.Error("Failed to wait for other instance");
			return false;
		}
	}
}