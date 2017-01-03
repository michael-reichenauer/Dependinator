using System;
using Dependiator.MainViews;


namespace Dependiator.Modeling
{
	internal class ModuleNameViewModel : ItemViewModel
	{
		private readonly ModuleName moduleName;

		public ModuleNameViewModel(ModuleName moduleName)
			: base(moduleName)
		{
			this.moduleName = moduleName;
		}


		public string Name => moduleName.Name;

		public int FontSize
		{
			get
			{
				int fontSize = (int)(12 * moduleName.ParentNode.Scale);
				fontSize = Math.Max(8, fontSize);
				return Math.Min(20, fontSize);
			} 
		}
	}
}