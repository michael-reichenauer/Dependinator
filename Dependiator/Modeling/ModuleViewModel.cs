using System.Windows.Media;
using Dependiator.MainViews;


namespace Dependiator.Modeling
{
	internal class ModuleViewModel : ItemViewModel
	{
		private readonly Module module;

		public ModuleViewModel(Module module)
			: base(module)
		{
			this.module = module;
		}


		public int StrokeThickness => 1;
		public double RectangleWidth => module.ItemBounds.Width * module.Scale - StrokeThickness * 2;
		public double RectangleHeight => module.ItemBounds.Height * module.Scale - StrokeThickness * 2;
		public Brush RectangleBrush => module.RectangleBrush;
		public Brush HoverBrush => module.RectangleBrush;




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

	}
}