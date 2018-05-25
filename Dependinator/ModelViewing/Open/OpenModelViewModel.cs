using System.Collections.Generic;
using System.IO;
using System.Windows;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.Common.SettingsHandling;
using Dependinator.ModelViewing.Items;


namespace Dependinator.ModelViewing.Open
{
	internal class OpenModelViewModel : ItemViewModel
	{
		private static readonly Rect DefaultOpenModelNodeBounds = new Rect(30, 30, 730, 580);

		private readonly IOpenModelService openModelService;
		private readonly IRecentModelsService recentModelsService;


		public OpenModelViewModel(
			IOpenModelService openModelService,
			IRecentModelsService recentModelsService)
		{
			this.openModelService = openModelService;
			this.recentModelsService = recentModelsService;
			ItemBounds = DefaultOpenModelNodeBounds;

			RecentFiles = GetRecentFiles();
		}


		public IReadOnlyList<FileItem> RecentFiles { get; }


		public async void OpenFile() => await openModelService.OpenOtherModelAsync();


		public async void OpenExampleFile()
		{
			string dataFolderPath = ProgramInfo.GetProgramDataFolderPath();
	
			string examplePath = Path.Combine(dataFolderPath, "Example", "Example.exe");

			await openModelService.TryModelAsync(examplePath);
		}


		private IReadOnlyList<FileItem> GetRecentFiles()
		{
			IReadOnlyList<string> filesPaths = recentModelsService.GetModelPaths();

			var fileItems = new List<FileItem>();
			foreach (string filePath in filesPaths)
			{
				string name = Path.GetFileName(filePath);

				fileItems.Add(new FileItem(name, filePath, openModelService.TryModelAsync));
			}

			return fileItems;
		}
	}
}