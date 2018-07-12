using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Runtime.Serialization.Formatters;
using System.Threading;


namespace Dependinator.Utils.Net
{
	internal class IpcServerRemotingService : IDisposable
	{
		private string uniqueId;
		private Mutex channelMutex;
		private IpcServerChannel ipcServerChannel;


		public bool TryCreateServer(string serverId)
		{
			uniqueId = serverId;
			string mutexName = GetId(serverId);
			string channelName = GetChannelName(serverId);

			channelMutex = new Mutex(true, mutexName, out var isMutexCreated);
			if (isMutexCreated)
			{
				CreateIpcServer(channelName);
			}

			Log.Debug($"Created {mutexName} = {isMutexCreated}");
			return isMutexCreated;
		}


		public static bool IsServerRegistered(string serverId)
		{
			string mutexName = GetId(serverId);

			using (new Mutex(true, mutexName, out var isMutexCreated))
			{
				if (isMutexCreated)
				{
					return false;
				}
			}

			return true;
		}


		public void PublishService<T>(IpcService ipcService)
		{
			Asserter.Requires(ipcServerChannel != null);

			// Publish the ipc service receiving the data
			string ipcServiceName = uniqueId + typeof(T).FullName;
			RemotingServices.Marshal(ipcService, ipcServiceName);
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


		private void CreateIpcServer(string channelName)
		{
			BinaryServerFormatterSinkProvider serverProvider = new BinaryServerFormatterSinkProvider();
			serverProvider.TypeFilterLevel = TypeFilterLevel.Full;

			IDictionary properties = new Dictionary<string, string>();
			properties["name"] = channelName;
			properties["portName"] = channelName;
			properties["exclusiveAddressUse"] = "false";

			ipcServerChannel = new IpcServerChannel(properties, serverProvider);
			ChannelServices.RegisterChannel(ipcServerChannel, true);
		}

	

		private static string GetChannelName(string uniqueName) => $"{GetId(uniqueName)}:rpc";

		private static string GetId(string uniqueName) => uniqueName + Environment.UserName;
	}
}