using System;
using System.Collections.Generic;
using System.Windows.Media;
using Dependiator.MainViews;
using Dependiator.Utils.UI;


namespace Dependiator.Modeling
{
	internal class Module : Item
	{
		private readonly ICanvasService canvasService;


		public Module(ICanvasService canvasService)		
		{
			this.canvasService = canvasService;
			ZIndex = 200;
		}

		public override ItemViewModel ViewModelFactory() => new ModuleViewModel(this, canvasService);
		
		public override void ZoomChanged()
		{
			ModuleViewModel.NotifyAll();
		}


		public ModuleName Name { get; set; }

		public Module Parent { get; set; }

		public List<Module> Children { get; } = new List<Module>();

		public ModuleViewModel ModuleViewModel => ViewModel as ModuleViewModel;
		public SolidColorBrush RectangleBrush { get; set; }
	}
}