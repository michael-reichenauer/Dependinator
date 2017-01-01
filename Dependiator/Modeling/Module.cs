using System;
using System.Collections.Generic;
using System.Windows.Media;
using Dependiator.MainViews;


namespace Dependiator.Modeling
{
	internal class Module : Item
	{
		private readonly Lazy<ModuleViewModel> viewModel;

		public Module(ICanvasService canvasService)
		{
			viewModel = new Lazy<ModuleViewModel>(() => new ModuleViewModel(this, canvasService));
			ZIndex = 200;
		}

		public override object ViewModel => viewModel.Value;


		public override void ZoomChanged()
		{
			ModuleViewModel.NotifyAll();
		}


		//public ModuleName Name { get; set; }

		public Module Parent { get; set; }

		public List<Module> Children { get; } = new List<Module>();


		public ModuleViewModel ModuleViewModel => viewModel.Value;
		public SolidColorBrush RectangleBrush { get; set; }
	}
}