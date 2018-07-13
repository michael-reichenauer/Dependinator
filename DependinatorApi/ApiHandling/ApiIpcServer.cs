using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Runtime.Serialization.Formatters;
using System.Threading;
using DependinatorApi.ApiHandling.Private;


namespace DependinatorApi.ApiHandling
{
	public class ApiIpcServer : IDisposable
	{
		private readonly string serverId;
		private Mutex channelMutex;
		private IpcServerChannel ipcServerChannel;


		public ApiIpcServer(string serverName)
		{
			this.serverId = ApiIpcCommon.GetServerId(serverName);
		}


		public static bool IsServerRegistered(string serverName) => ApiIpcCommon.IsServerRegistered(serverName);


		public bool TryPublishService<TRemoteService>(ApiIpcService ipcService)
		{
			if (channelMutex == null)
			{
				if (!TryCreateServer())
				{
					return false;
				}
			}

			// Publish the ipc service receiving the data
			string ipcServiceName = ApiIpcCommon.GetServiceName<TRemoteService>(serverId);
			RemotingServices.Marshal(ipcService, ipcServiceName);

			return true;
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
			catch (Exception e)
			{
				Log.Warn($"Failed to close RPC remoting service {e}");
			}
		}



		private bool TryCreateServer()
		{
			string mutexName = (serverId);
			string channelName = serverId;

			channelMutex = new Mutex(true, mutexName, out var isMutexCreated);
			if (!isMutexCreated)
			{
				channelMutex?.Dispose();
				channelMutex = null;
				return false;
			}

			ipcServerChannel = CreateIpcServerChannel(channelName);
			return true;
		}


		private static IpcServerChannel CreateIpcServerChannel(string channelName)
		{
			BinaryServerFormatterSinkProvider sinkProvider = new BinaryServerFormatterSinkProvider
			{
				TypeFilterLevel = TypeFilterLevel.Full
			};

			IDictionary properties = new Dictionary<string, string>
			{
				["name"] = channelName,
				["portName"] = channelName,
				["exclusiveAddressUse"] = "false"
			};

			IpcServerChannel ipcServerChannel = new IpcServerChannel(properties, sinkProvider);
			ChannelServices.RegisterChannel(ipcServerChannel, true);

			return ipcServerChannel;
		}
	}
}