using System.Collections.Generic;
using System.IO;
using System.Windows;
using Dependinator.Common.ModelMetadataFolders.Private;
using Dependinator.ModelViewing;


namespace Dependinator.Common.ModelMetadataFolders
{
    internal class OpenModelViewModel : ItemViewModel
    {
        private static readonly Rect DefaultOpenModelNodeBounds = new Rect(0, 80, 720, 580);

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
            string installFolderPath = ProgramInfo.GetInstallFolderPath();

            string examplePath = Path.Combine(installFolderPath, "Example", "Example.exe");

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
