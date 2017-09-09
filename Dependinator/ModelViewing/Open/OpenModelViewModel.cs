using System.Collections.Generic;
using System.IO;
using System.Windows;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.ModelViewing.Private.Items;

namespace Dependinator.ModelViewing.Open
{
	internal class OpenModelViewModel : ItemViewModel
	{
		private static readonly Rect DefaultOpenModelNodeBounds = new Rect(30, 30, 730, 580);

		private readonly IOpenModelService openModelService;


		public OpenModelViewModel(IOpenModelService openModelService)
		{
			this.openModelService = openModelService;
			ItemBounds = DefaultOpenModelNodeBounds;

			RecentFiles = GetRecentFiles();
		}


		public IReadOnlyList<FileItem> RecentFiles { get; }


		public async void OpenFile() => await openModelService.OpenModelAsync();


		private IReadOnlyList<FileItem> GetRecentFiles()
		{
			IReadOnlyList<string> filesPaths = openModelService.GetResentModelFilePaths();

			var fileItems = new List<FileItem>();
			foreach (string filePath in filesPaths)
			{
				string name = Path.GetFileName(filePath);

				fileItems.Add(new FileItem(name, filePath, openModelService.OpenModelAsync));
			}

			return fileItems;
		}
	}
}