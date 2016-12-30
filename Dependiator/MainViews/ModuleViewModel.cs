using System;
using System.Windows;
using System.Windows.Media;
using Dependiator.MainViews.Private;
using Dependiator.Utils.UI;


namespace Dependiator.MainViews
{
	internal class Module : IVirtualItem
	{
		private readonly Lazy<ModuleViewModel> viewModel;

		public Module()
		{
			viewModel = new Lazy<ModuleViewModel>(() => new ModuleViewModel(this));
		}

		public object VirtualId { get; set; }
		public Rect ItemBounds { get; set; }

		public object ViewModel => viewModel.Value;

		public ModuleViewModel ModuleViewModel => viewModel.Value;
		public SolidColorBrush RectangleBrush { get; set; }

		public int ZIndex = 200;
		
	}


	internal class ModuleViewModel : ViewModel
	{
		private readonly Module module;

		public ModuleViewModel(Module module)
		{
			this.module = module;
		}

		// UI properties
		public string Type => nameof(ModuleViewModel);
		public int CanvasZIndex => module.ZIndex;
		public double CanvasWidth => module.ItemBounds.Width;
		public double CanvasTop => module.ItemBounds.Top;
		public double CanvasLeft => module.ItemBounds.Left;
		public double CanvasHeight => module.ItemBounds.Height;

		public int StrokeThickness => 1;
		public int RectangleWidth => (int)module.ItemBounds.Width - StrokeThickness * 2;
		public int RectangleHeight => (int)module.ItemBounds.Height - StrokeThickness * 2;
		public Brush RectangleBrush => module.RectangleBrush;




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