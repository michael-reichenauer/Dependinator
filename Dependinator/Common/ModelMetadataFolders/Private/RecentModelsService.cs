using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.Common.SettingsHandling;
using Dependinator.Utils;


namespace Dependinator.Common.ModelMetadataFolders.Private
{
	[SingleInstance]
	internal class RecentModelsService : IRecentModelsService
	{
		private readonly ISettingsService settingsService;
		private readonly IJumpListService jumpListService;


		public RecentModelsService(
			ISettingsService settingsService,
			IJumpListService jumpListService)
		{
			this.settingsService = settingsService;
			this.jumpListService = jumpListService;
		}


		public event EventHandler Changed;
		
		public void AddModelPaths(string modelFilePath)
		{
			AddToResentPathInProgramSettings(modelFilePath);

			jumpListService.AddPath(modelFilePath);
			Changed?.Invoke(this, EventArgs.Empty);
		}


		public IReadOnlyList<string> GetModelPaths()
		{
			ProgramSettings settings = settingsService.Get<ProgramSettings>();

			return settings.ResentModelPaths.ToList();
		}


		public void RemoveModelPath(string modelFilePath)
		{
			List<string> resentPaths = settingsService.Get<ProgramSettings>().ResentModelPaths.ToList();
			int index = resentPaths.FindIndex(path => path.IsSameIgnoreCase(modelFilePath));

			if (index != -1)
			{
				resentPaths.RemoveAt(index);
			}

			settingsService.Edit<ProgramSettings>(s => { s.ResentModelPaths = resentPaths; });
		}


		private void AddToResentPathInProgramSettings(string modelFilePath)
		{
			ProgramSettings settings = settingsService.Get<ProgramSettings>();

			List<string> resentPaths = settings.ResentModelPaths;
			int index = resentPaths.FindIndex(path => path.IsSameIgnoreCase(modelFilePath));

			if (index != -1)
			{
				resentPaths.RemoveAt(index);
			}

			resentPaths.Insert(0, modelFilePath);

			if (resentPaths.Count > 10)
			{
				resentPaths.RemoveRange(10, resentPaths.Count - 10);
			}

			settingsService.Edit<ProgramSettings>(s => { s.ResentModelPaths = resentPaths; });
		}
	}
}