using System;
using Dependinator.Common.ModelMetadataFolders.Private;
using Dependinator.Utils;
using Dependinator.Utils.Dependencies;
using DependinatorApi;
using DependinatorApi.ApiHandling;


namespace Dependinator.Api.ApiHandling.Private
{
	[SingleInstance]
	internal class ApiManagerService : IApiManagerService
	{
		private readonly IModelMetadataService modelMetadataService;
		private readonly DependinatorApiService dependinatorApiService;
		private ApiIpcServer apiIpcServer;


		public ApiManagerService(
			IModelMetadataService modelMetadataService,
			DependinatorApiService dependinatorApiService)
		{
			this.modelMetadataService = modelMetadataService;
			this.dependinatorApiService = dependinatorApiService;
		}
		

		public void Register()
		{
			try
			{
				string serverName = GetCurrentInstanceServerName();

				apiIpcServer?.Dispose();
				apiIpcServer = new ApiIpcServer(serverName);

				if (!apiIpcServer.TryPublishService<IDependinatorApi>(dependinatorApiService))
				{
					throw new ApplicationException($"Failed to register rpc instance {serverName}");
				}

				Log.Info($"Registered: {serverName}");
			}
			catch (Exception e)
			{
				Log.Exception(e);
				throw;
			}
		}


		public string GetCurrentInstanceServerName() =>
			$"{ProgramInfo.Name}:{modelMetadataService.MetadataFolderPath.ToLower()}";
	}
}