using System;
using System.Windows;
using Dependiator.MainViews;
using Dependiator.MainViews.Private;
using Dependiator.Utils.UI;


namespace Dependiator.Modeling
{
	internal class ModuleName : Item
	{
		private readonly ICanvasService canvasService;

	
		public ModuleName(ICanvasService canvasService)
		{
			this.canvasService = canvasService;		
		}


		public override void ZoomChanged() => ViewModel.NotifyAll();

		public override ItemViewModel ViewModelFactory() => new ModuleNameViewModel(this, canvasService);

		public string Name { get; set; }
	}
}