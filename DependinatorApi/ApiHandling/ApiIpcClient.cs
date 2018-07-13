using System;
using System.Collections.Generic;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using DependinatorApi.ApiHandling.Private;


namespace DependinatorApi.ApiHandling
{
	public class ApiIpcClient : IDisposable
	{
		private readonly string serverId;

		private IpcClientChannel ipcClientChannel;
		private readonly Dictionary<Type, object> proxies = new Dictionary<Type, object>();


		public ApiIpcClient(string serverName)
		{
			this.serverId = ApiIpcCommon.GetServerId(serverName);
			Log.Debug($"Create client: {serverName} as {serverId}");
		}


		public static bool IsServerRegistered(string serverName) => ApiIpcCommon.IsServerRegistered(serverName);


		public TRemoteService Service<TRemoteService>()
		{
			if (proxies.TryGetValue(typeof(TRemoteService), out object ipcProxy))
			{
				return (TRemoteService) ipcProxy;
			}

			if (ipcClientChannel == null)
			{
				ipcClientChannel = new IpcClientChannel();

				ChannelServices.RegisterChannel(ipcClientChannel, true);
			}

			string ipcServiceName = ApiIpcCommon.GetServiceName<TRemoteService>(serverId);
			Log.Debug($"Calling: {ipcServiceName}");

			string ipcUrl = $"ipc://{serverId}/{ipcServiceName}";

			// Get proxy instance of rpc service instance published by server in PublishService()
			ipcProxy = RemotingServices.Connect(typeof(TRemoteService), ipcUrl);
			proxies[typeof(TRemoteService)] = ipcProxy;

			if (ipcProxy == null)
			{
				Log.Error("Failed to create IPC proxy");
			}

			return (TRemoteService)ipcProxy;
		}


		public void Dispose()
		{
			try
			{
				if (ipcClientChannel != null)
				{
					ChannelServices.UnregisterChannel(ipcClientChannel);
					ipcClientChannel = null;
				}
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to close RPC remoting service {e}");
			}
		}
	}
}