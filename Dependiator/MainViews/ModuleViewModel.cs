using System.Windows;
using System.Windows.Media;
using Dependiator.Utils.UI;


namespace Dependiator.MainViews
{
	internal class ModuleViewModel : ViewModel
	{
		//private readonly IThemeService themeService;
		//private readonly IRepositoryCommands repositoryCommands;

		//public ModuleViewModel(
		//	IThemeService themeService,
		//	IRepositoryCommands repositoryCommands)
		//{
		//	this.themeService = themeService;
		//	this.repositoryCommands = repositoryCommands;
		//}

		// UI properties
		public string Type => nameof(ModuleViewModel);
		public int ZIndex => 200;

		public Rect Rect { get; set; }
		public Rect Rectangle { get; set; }
		public Brush Brush { get; set; }

		public double Width => Rect.Width;
		public double Top => Rect.Top;
		public double Left => Rect.Left;
		public double Height => Rect.Height;

		

		//public string Id => Branch.Id;
		//public string Name => Branch.Name;
		//public string Dashes { get; set; }
		//public int NeonEffect { get; set; }
		//public int StrokeThickness { get; set; }





		//public Command ChangeColorCommand => Command(() =>
		//{
		//	themeService.ChangeBranchBrush(Branch);
		//	repositoryCommands.RefreshView();
		//});

	


		public override string ToString() => $"Module";
	}
}