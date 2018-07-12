using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Runtime.Serialization.Formatters;
using System.Security.Cryptography;
using System.Text;
using System.Threading;


namespace DependinatorApi.ApiHandling
{
	public class ApiIpcServer : IDisposable
	{
		private static readonly string ApiGuid = "AAFB58B3-34AF-408B-92BD-55DC977E5250";

		private readonly string serverId;
		private Mutex channelMutex;
		private IpcServerChannel ipcServerChannel;


		public ApiIpcServer(string serverId)
		{
			this.serverId = GetUniqueId(serverId);
		}


		public static bool IsServerRegistered(string serverId)
		{
			serverId = GetUniqueId(serverId);
			using (new Mutex(true, serverId, out var isMutexCreated))
			{
				if (isMutexCreated)
				{
					return false;
				}
			}

			return true;
		}


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
			string ipcServiceName = serverId + typeof(TRemoteService).FullName;
			RemotingServices.Marshal(ipcService, ipcServiceName);

			return true;
		}


		internal static string GetUniqueId(string text) =>
			AsSha2Text(ApiGuid + Environment.UserName + text.ToLower());


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

			ipcServerChannel = CreateIpcServer(channelName);
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


		private static IpcServerChannel CreateIpcServer(string channelName)
		{
			BinaryServerFormatterSinkProvider sinkProvider = new BinaryServerFormatterSinkProvider();
			sinkProvider.TypeFilterLevel = TypeFilterLevel.Full;

			IDictionary properties = new Dictionary<string, string>();
			properties["name"] = channelName;
			properties["portName"] = channelName;
			properties["exclusiveAddressUse"] = "false";

			IpcServerChannel ipcServerChannel = new IpcServerChannel(properties, sinkProvider);
			ChannelServices.RegisterChannel(ipcServerChannel, true);

			return ipcServerChannel;
		}


		private static string AsSha2Text(string text)
		{
			byte[] textBytes = Encoding.UTF8.GetBytes(text.ToLower());

			SHA256Managed shaService = new SHA256Managed();
			byte[] shaHash = shaService.ComputeHash(textBytes, 0, textBytes.Length);

			StringBuilder hashText = new StringBuilder();
			foreach (byte b in shaHash)
			{
				hashText.Append(b.ToString("x2"));
			}

			return hashText.ToString();
		}
	}
}