using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Dependinator.Utils;
using Dependinator.Utils.Dependencies;
using Dependinator.Utils.Net;
using Dependinator.Utils.Threading;


namespace Dependinator.Common.ModelMetadataFolders.Private
{
	[SingleInstance]
	internal class ExistingInstanceService : IExistingInstanceService
	{
		private readonly IModelMetadataService modelMetadataService;
		private readonly ExistingInstanceIpcService existingInstanceIpcService;
		private IpcServerRemotingService instanceIpcServerRemotingService;


		public ExistingInstanceService(
			IModelMetadataService modelMetadataService,
			ExistingInstanceIpcService existingInstanceIpcService)
		{
			this.modelMetadataService = modelMetadataService;
			this.existingInstanceIpcService = existingInstanceIpcService;
		}


		public void RegisterPath(string metaDataFolderPath)
		{
			try
			{
				instanceIpcServerRemotingService?.Dispose();

				instanceIpcServerRemotingService = new IpcServerRemotingService();

				string id = GetMetadataFolderId(metaDataFolderPath);

				if (!instanceIpcServerRemotingService.TryCreateServer(id))
				{
					throw new ApplicationException($"Failed to register rpc instance {metaDataFolderPath}");
				}

				Log.Debug($"$Register {id}");
				instanceIpcServerRemotingService.PublishService<ExistingInstanceIpcService>(existingInstanceIpcService);
			}
			catch (Exception e)
			{
				Log.Exception(e);
				throw;
			}
		}


		public bool TryActivateExistingInstance(string metaDataFolderPath, string[] args)
		{
			try
			{
				// Trying to contact another instance, which has a registered IpcRemotingService 
				string id = GetMetadataFolderId(metaDataFolderPath);
				using (IpcClientRemotingService ipcRemotingService = new IpcClientRemotingService())
				{
					if (ipcRemotingService.IsServerRegistered(id))
					{
						// Another instance for that working folder is already running, activate that.
						IExistingInstanceIpcService service = ipcRemotingService
							.GetService<ExistingInstanceIpcService>(id);
						service.Activate(args);

						return true;
					}
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
					string id = GetMetadataFolderId(modelMetadataService.MetadataFolderPath);
					using (IpcServerRemotingService ipcRemotingService = new IpcServerRemotingService())
					{
						if (ipcRemotingService.TryCreateServer(id))
						{
							Log.Debug("Other instance has closed");
							return true;
						}

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



		private static string GetMetadataFolderId(string metadataFolderPath)
		{
			string name = ProgramInfo.Guid + Uri.EscapeDataString(metadataFolderPath);

			string id = AsSha2Text(name);
			return id;
		}


		private static string AsSha2Text(string text)
		{
			SHA256Managed shaService = new SHA256Managed();
			StringBuilder hashText = new StringBuilder();

			byte[] textBytes = Encoding.UTF8.GetBytes(text.ToLower());

			byte[] shaHash = shaService.ComputeHash(textBytes, 0, textBytes.Length);

			shaHash.ForEach(b => hashText.Append(b.ToString("x2")));
			
			return hashText.ToString();
		}
	}
}