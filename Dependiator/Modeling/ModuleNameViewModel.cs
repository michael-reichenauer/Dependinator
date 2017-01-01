using System;
using Dependiator.MainViews;
using Dependiator.Utils.UI;


namespace Dependiator.Modeling
{
	internal class ModuleNameViewModel : ItemViewModel
	{
		private readonly ModuleName moduleName;
		private readonly ICanvasService canvasService;


		public ModuleNameViewModel(ModuleName moduleName, ICanvasService canvasService)
			: base(moduleName)
		{
			this.moduleName = moduleName;
			this.canvasService = canvasService;
		}


		public string Name => moduleName.Name;

		public int FontSize
		{
			get
			{
				int fontSize = (int)(12 * canvasService.Scale);
				fontSize = Math.Max(8, fontSize);
				return Math.Min(40, fontSize);
			} 
		}
	}
}