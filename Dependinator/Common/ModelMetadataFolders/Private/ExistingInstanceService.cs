using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dependinator.Utils;


namespace Dependinator.Common.ModelMetadataFolders.Private
{
	[SingleInstance]
	internal class ExistingInstanceService : IExistingInstanceService
	{
		private readonly ExistingInstanceIpcService existingInstanceIpcService;
		private IpcRemotingService instanceIpcRemotingService;


		public ExistingInstanceService(ExistingInstanceIpcService existingInstanceIpcService)
		{
			this.existingInstanceIpcService = existingInstanceIpcService;
		}


		public void RegisterPath(string metaDataFolderPath)
		{
			try
			{
				instanceIpcRemotingService?.Dispose();

				instanceIpcRemotingService = new IpcRemotingService();

				string id = GetMetadataFolderId(metaDataFolderPath);

				if (!instanceIpcRemotingService.TryCreateServer(id))
				{
					throw new ApplicationException($"Failed to register rpc instance {metaDataFolderPath}");
				}

				instanceIpcRemotingService.PublishService(existingInstanceIpcService);
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
				using (IpcRemotingService ipcRemotingService = new IpcRemotingService())
				{
					if (ipcRemotingService.IsServerRegistered(id))
					{
						// Another instance for that working folder is already running, activate that.
						ExistingInstanceIpcService service = ipcRemotingService
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


		private static string GetMetadataFolderId(string metadataFolderPath)
		{
			string name = Product.Guid + Uri.EscapeDataString(metadataFolderPath);

			string id = AsSha2Text(name);
			return id;
		}


		private static string AsSha2Text(string text)
		{
			SHA256Managed shaService = new SHA256Managed();
			StringBuilder hashText = new StringBuilder();

			byte[] textBytes = Encoding.UTF8.GetBytes(text);

			byte[] shaHash = shaService.ComputeHash(textBytes, 0, textBytes.Length);

			shaHash.ForEach(b => hashText.Append(b.ToString("x2")));
			
			return hashText.ToString();
		}
	}
}