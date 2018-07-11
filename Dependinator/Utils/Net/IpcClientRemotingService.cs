using System;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Threading;


namespace Dependinator.Utils.Net
{
	internal class IpcClientRemotingService : IDisposable
	{
		private Mutex channelMutex;
		private IpcServerChannel ipcServerChannel;
		private IpcClientChannel ipcClientChannel;


		public bool IsServerRegistered(string serverId)
		{
			string mutexName = GetId(serverId);

			Log.Debug($"Try {mutexName}");
			
			using (new Mutex(true, mutexName, out var isMutexCreated))
			{
				if (isMutexCreated)
				{
					return false;
				}
			}

			Log.Debug($"Server {mutexName} exist");

			return true;
		}



		public TRemoteService GetService<TRemoteService>(string serverId)
		{
			return CreateClientProxy<TRemoteService>(serverId);
		}


		public void CallService<TRemoteService>(
			string serverId, Action<TRemoteService> serviceAction)
		{
			TRemoteService ipcProxy = CreateClientProxy<TRemoteService>(serverId);

			serviceAction(ipcProxy);
		}


		public TResult CallService<TRemoteService, TResult>(
			string serverId, Func<TRemoteService, TResult> serviceFunction)
		{
			TRemoteService ipcProxy = CreateClientProxy<TRemoteService>(serverId);

			return serviceFunction(ipcProxy);
		}


		public void Dispose()
		{
			try
			{
				if (channelMutex != null)
				{
					channelMutex.Close();
					channelMutex = null;
				}

				if (ipcServerChannel != null)
				{
					ChannelServices.UnregisterChannel(ipcServerChannel);
					ipcServerChannel = null;
				}
			}
			catch (Exception e) when (e.IsNotFatal())
			{
				Log.Warn("Failed to close RPC remoting service");
			}
		}


		private T CreateClientProxy<T>(string id)
		{
			string channelName = GetChannelName(id);

			if (ipcClientChannel == null)
			{
				ipcClientChannel = new IpcClientChannel();

				ChannelServices.RegisterChannel(ipcClientChannel, true);
			}

			string ipcServiceName = id + typeof(T).FullName;
			string ipcUrl = $"ipc://{channelName}/{ipcServiceName}";

			// Get proxy instance of rpc service instance published by server in PublishService()
			T ipcProxy = (T)RemotingServices.Connect(typeof(T), ipcUrl);

			if (ipcProxy == null)
			{
				Log.Error($"Failed to makeIPC call {channelName}");
			}
		
			return ipcProxy;
		}


		private static string GetChannelName(string uniqueName) => $"{GetId(uniqueName)}:rpc";

		private static string GetId(string uniqueName) => uniqueName + Environment.UserName;
	}
}