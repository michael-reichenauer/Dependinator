using System.Threading.Tasks;
using System.Windows;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.Common.ProgressHandling;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.Private.CodeViewing.Private
{
    internal class CodeViewModel : ViewModel
    {
        private readonly Window owner;
        private readonly ISolutionService solutionService;
        private readonly IProgressService progressService;
        private readonly ModelPaths modelPaths;



        public CodeViewModel(
            ISolutionService solutionService,
            IProgressService progressService,
            ModelPaths modelPaths,
            string title,
            Window owner)
        {
            this.solutionService = solutionService;
            this.progressService = progressService;
            this.modelPaths = modelPaths;
            this.owner = owner;

            Title = title;
        }


        public string FilePath { get => Get(); set => Set(value).Notify(nameof(IsShowOpenInStudioButton)); }

        public int LineNumber { get; set; }

        public Command<Window> CancelCommand => Command<Window>(w => w.Close());

        public Command OpenInStudioCommand => AsyncCommand(OpenInStudio);

        public bool IsShowOpenInStudioButton => !string.IsNullOrEmpty(FilePath);


        public string Title { get => Get(); set => Set(value); }


        private async Task OpenInStudio()
        {
            if (FilePath == null) return;

            owner.Close();
            await Task.Yield();

            using (progressService.ShowDialog("Opening file in Visual Studio..."))
            {
                await solutionService.OpenFileAsync(modelPaths, FilePath, LineNumber);
            }
        }
    }
}
