using System;
using System.Windows;
using Dependinator.Common.SettingsHandling;
using Dependinator.Utils;


namespace Dependinator.Common.ModelMetadataFolders.Private
{
	[SingleInstance]
	internal class ExistingInstanceService : IExistingInstanceService
	{
		private readonly ExistingInstanceIpcService existingInstanceIpcService;
		private IpcRemotingService ipcRemotingService;


		public ExistingInstanceService(ExistingInstanceIpcService existingInstanceIpcService)
		{
			this.existingInstanceIpcService = existingInstanceIpcService;
		}


		public bool TryRegisterPath(string metaDataFolderPath)
		{
			ipcRemotingService?.Dispose();

			ipcRemotingService = new IpcRemotingService();

			string id = ProgramInfo.GetMetadataFolderId(metaDataFolderPath);

			if (ipcRemotingService.TryCreateServer(id))
			{
				ipcRemotingService.PublishService(existingInstanceIpcService);
				return true;
			}

			// Another instance for that working folder is already running, activate that.
			ExistingInstanceIpcService service = ipcRemotingService
				.GetService<ExistingInstanceIpcService>(id);

			service.Activate(null);

			// Trigger shutdown of this instance
			Application.Current.Shutdown(0);
			ipcRemotingService.Dispose();
			return false;
		}
	}
}