using System.Threading.Tasks;
using System.Windows;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.Private.CodeViewing.Private
{
    internal class CodeViewModel : ViewModel
    {
        private readonly Window owner;
        private readonly ISolutionService solutionService;

        public CodeViewModel(
            ISolutionService solutionService,
            string title,
            Window owner)
        {
            this.solutionService = solutionService;
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
            if (FilePath != null)
            {
                await solutionService.OpenFileAsync(FilePath, LineNumber);

                owner.Close();
            }
        }
    }
}
