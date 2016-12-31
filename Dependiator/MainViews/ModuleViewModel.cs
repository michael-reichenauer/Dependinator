using System.Windows.Media;
using Dependiator.Utils.UI;


namespace Dependiator.MainViews
{
	internal class ModuleViewModel : ViewModel
	{
		private readonly Module module;
		private readonly ICanvasService canvasService;


		public ModuleViewModel(Module module, ICanvasService canvasService)
		{
			this.module = module;
			this.canvasService = canvasService;
		}

		// UI properties
		public string Type => nameof(ModuleViewModel);
		public int CanvasZIndex => module.ZIndex;
		public double CanvasWidth => module.ItemBounds.Width;
		public double CanvasTop => module.ItemBounds.Top;
		public double CanvasLeft => module.ItemBounds.Left;
		public double CanvasHeight => module.ItemBounds.Height;

		public int StrokeThickness => 1;
		public double RectangleWidth => module.ItemBounds.Width * canvasService.Scale - StrokeThickness * 2;
		public double RectangleHeight => module.ItemBounds.Height * canvasService.Scale - StrokeThickness * 2;
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




		public override string ToString() => $"Module";
	}
}