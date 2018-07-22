using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Runtime.Serialization.Formatters;
using System.Threading;
using System.Threading.Tasks;
using DependinatorApi.ApiHandling.Private;


namespace DependinatorApi.ApiHandling
{
	/// <summary>
	/// IPC server used to publish IPC api services, which can be called by ApiIpcClient.
	/// </summary>
	public class ApiIpcServer : IDisposable
	{
		private readonly string serverId;
		private Mutex channelMutex;
		private IpcServerChannel ipcServerChannel;


		public ApiIpcServer(string serverName)
		{
			this.serverId = ApiIpcCommon.GetServerId(serverName);
			Log.Debug($"Create server: {serverName} as {serverId}");
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

			Log.Debug($"Published: {ipcServiceName}");
			return true;
		}



		public async Task PublishServiceAsync<TRemoteService>(ApiIpcService ipcService)
		{
			if (channelMutex == null)
			{
				await CreateServerAsync();
			}

			// Publish the ipc service receiving the data
			string ipcServiceName = ApiIpcCommon.GetServiceName<TRemoteService>(serverId);
			RemotingServices.Marshal(ipcService, ipcServiceName);

			Log.Debug($"Published: {ipcServiceName}");
		}



		public void Dispose()
		{
			try
			{
				if (ipcServerChannel != null)
				{
					ChannelServices.UnregisterChannel(ipcServerChannel);
					ipcServerChannel = null;
				}

				if (channelMutex != null)
				{
					channelMutex.Close();
					channelMutex = null;
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


		private async Task CreateServerAsync()
		{
			string mutexName = (serverId);
			string channelName = serverId;

			channelMutex = await CreateMutexAsync(mutexName);
			
			ipcServerChannel = CreateIpcServerChannel(channelName);
		}


		private static async Task<Mutex> CreateMutexAsync(string mutexName)
		{
			while (true)
			{
				Mutex mutex = new Mutex(true, mutexName, out var isMutexCreated);
				if (isMutexCreated)
				{
					return mutex;
				}

				// The mutex was already locked by some other thread/process, lets await it being unlocked
				TaskInfo taskInfo = new TaskInfo(mutex);
				taskInfo.Handle = ThreadPool.RegisterWaitForSingleObject(mutex, OnMutexReleased, taskInfo, -1, true);

				await taskInfo.Tcs.Task;
			}
		}


		private static void OnMutexReleased(object state, bool timedOut)
		{
			TaskInfo taskInfo = (TaskInfo)state;

			// Clean
			taskInfo.Handle.Unregister(taskInfo.Mutex);
			taskInfo.Mutex.Dispose();

			// Signal that mutex is no longer locked
			taskInfo.Tcs.SetResult(true);
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


		private class TaskInfo
		{
			public Mutex Mutex { get; }
			public RegisteredWaitHandle Handle { get; set; }
			public TaskCompletionSource<bool> Tcs { get; } = new TaskCompletionSource<bool>();

			public TaskInfo(Mutex mutex) => Mutex = mutex;
		}
	}
}