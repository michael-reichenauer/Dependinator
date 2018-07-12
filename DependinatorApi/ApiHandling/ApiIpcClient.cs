using System;
using System.Collections.Generic;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;


namespace DependinatorApi.ApiHandling
{
	public class ApiIpcClient : IDisposable
	{
		private readonly string serverId;

		private IpcClientChannel ipcClientChannel;
		private readonly Dictionary<Type, object> proxies = new Dictionary<Type, object>();


		public ApiIpcClient(string serverId)
		{
			this.serverId = ApiIpcServer.GetUniqueId(serverId);
		}


		public static bool IsServerRegistered(string serverId) => ApiIpcServer.IsServerRegistered(serverId);



		public TRemoteService Service<TRemoteService>()
		{
			if (!proxies.TryGetValue(typeof(TRemoteService), out object ipcProxy))
			{
				if (ipcClientChannel == null)
				{
					ipcClientChannel = new IpcClientChannel();

					ChannelServices.RegisterChannel(ipcClientChannel, true);
				}

				string ipcServiceName = serverId + typeof(TRemoteService).FullName;
				string ipcUrl = $"ipc://{serverId}/{ipcServiceName}";

				// Get proxy instance of rpc service instance published by server in PublishService()
				ipcProxy = RemotingServices.Connect(typeof(TRemoteService), ipcUrl);
				proxies[typeof(TRemoteService)] = ipcProxy;

				if (ipcProxy == null)
				{
					Log.Error("Failed to create IPC proxy");
				}
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