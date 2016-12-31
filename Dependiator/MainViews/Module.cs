using System;
using System.Windows;
using System.Windows.Media;
using Dependiator.MainViews.Private;


namespace Dependiator.MainViews
{
	internal class Module : IVirtualItem
	{
		private readonly Lazy<ModuleViewModel> viewModel;

		public Module(ICanvasService canvasService)
		{
			viewModel = new Lazy<ModuleViewModel>(() => new ModuleViewModel(this, canvasService));
		}

		public object VirtualId { get; set; }
		public Rect ItemBounds { get; set; }

		public double Priority { get; set; }

		public void ZoomChanged()
		{
			ModuleViewModel.NotifyAll();
		}


		public object ViewModel => viewModel.Value;

		public ModuleViewModel ModuleViewModel => viewModel.Value;
		public SolidColorBrush RectangleBrush { get; set; }

		public int ZIndex = 200;
		
	}
}