using System.Windows;
using System.Windows.Media;
using Dependiator.Utils.UI;


namespace Dependiator.MainViews
{
	internal class ModuleViewModel : ViewModel
	{
		// UI properties
		public string Type => nameof(ModuleViewModel);
		public int CanvasZIndex => 200;
		public double CanvasWidth => CanvasBounds.Width;
		public double CanvasTop => CanvasBounds.Top;
		public double CanvasLeft => CanvasBounds.Left;
		public double CanvasHeight => CanvasBounds.Height;

		public int StrokeThickness => 1;
		public int RectangleWidth => (int)CanvasBounds.Width - StrokeThickness * 2;
		public int RectangleHeight => (int)CanvasBounds.Height - StrokeThickness * 2;
		public Brush RectangleBrush { get; set; }

		// Data
		public Rect CanvasBounds { get; set; }


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