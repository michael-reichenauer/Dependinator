using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.Common.ModelMetadataFolders.Private;
using Dependinator.Utils;
using Dependinator.Utils.Dependencies;
using DependinatorApi;
using DependinatorApi.ApiHandling;


namespace Dependinator.Common.ApiHandling.Private
{
    [SingleInstance]
    internal class ApiManagerService : IApiManagerService
    {
        private readonly IEnumerable<IApiIpcService> apiServices;
        private readonly IModelMetadataService modelMetadataService;
        private ApiIpcServer apiIpcServer;


        public ApiManagerService(
            IModelMetadataService modelMetadataService,
            IEnumerable<IApiIpcService> apiServices)
        {
            this.modelMetadataService = modelMetadataService;
            this.apiServices = apiServices;
        }


        public void Register()
        {
            try
            {
                foreach (IApiIpcService apiIpcService in apiServices)
                {
                    Type interfaceType = GetInterfaceType(apiIpcService);

                    string serverName = GetCurrentInstanceServerName(modelMetadataService.ModelFilePath);

                    apiIpcServer?.Dispose();
                    apiIpcServer = new ApiIpcServer(serverName);

                    if (!apiIpcServer.TryPublishService(interfaceType, apiIpcService))
                    {
                        throw new ApplicationException($"Failed to register rpc instance {serverName}");
                    }

                    Log.Info($"Registered: {serverName}");
                }
            }
            catch (Exception e)
            {
                Log.Exception(e);
                throw;
            }
        }


        public string GetCurrentInstanceServerName(string modelFilePath) =>
            ApiServerNames.ServerName<IDependinatorApi>(modelFilePath);


        public string GetCurrentInstanceServerName() =>
            ApiServerNames.ServerName<IDependinatorApi>(modelMetadataService.ModelFilePath);


        private static Type GetInterfaceType(IApiIpcService apiService) => 
            apiService.GetType().GetInterfaces().First(i => i != typeof(IApiIpcService));
    }
}
