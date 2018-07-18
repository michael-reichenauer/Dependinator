using System.Threading.Tasks;
using System.Windows;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.Private.CodeViewing.Private
{
	internal class CodeViewModel : ViewModel
	{
		private readonly ISolutionService solutionService;
		private readonly Window owner;


		public CodeViewModel(ISolutionService solutionService, string title, Window owner)
		{
			this.solutionService = solutionService;
			this.owner = owner;
			Title = title;
		}

		public string FilePath { get; set; }

		public int LineNumber { get; set; }

		public Command<Window> CancelCommand => Command<Window>(w => w.Close());


		public Command OpenInStudioCommand => AsyncCommand(OpenInStudio);


		private async Task OpenInStudio()
		{
			if (FilePath != null)
			{
				await solutionService.OpenFileAsync(FilePath, LineNumber);
				owner.Close();
			}
		}


		public string Title { get => Get(); set => Set(value); }
	}
}